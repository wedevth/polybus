namespace Polybus.RabbitMQ
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Google.Protobuf;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using WeDev.Extensions.Threading;

    public sealed class EventPublisher : EventBus, IAsyncDisposable, IDisposable, IEventPublisher
    {
        private readonly ILogger logger;
        private readonly SortedDictionary<DateTime, IModel> channels; // Free channel (not reserved).
        private readonly SortedSet<PendingMessage> pendings;
        private readonly ShutdownGuard shutdownGuard;
        private volatile bool disposed;

        public EventPublisher(IOptions<EventBusOptions> options, IConnection connection, ILogger<EventPublisher> logger)
            : base(options, connection)
        {
            this.logger = logger;
            this.channels = new SortedDictionary<DateTime, IModel>(Comparer<DateTime>.Create((l, r) => r.CompareTo(l)));
            this.pendings = new SortedSet<PendingMessage>();
            this.shutdownGuard = new ShutdownGuard();
        }

        public async ValueTask PublishAsync(IMessage @event, CancellationToken cancellationToken = default)
        {
            if (this.disposed || !this.shutdownGuard.TryAcquire())
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            try
            {
                var channel = this.ReserveChannel();

                try
                {
                    await this.PublishAsync(channel, @event, cancellationToken);
                }
                finally
                {
                    this.ReleaseChannel(channel);
                }
            }
            finally
            {
                this.shutdownGuard.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.shutdownGuard.PenetrateAsync().Wait();

                    this.CloseAllChannels();
                    this.LogUnconfirmedMessages();

                    this.shutdownGuard.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            if (!this.disposed)
            {
                await this.shutdownGuard.PenetrateAsync();

                this.CloseAllChannels();
                this.LogUnconfirmedMessages();

                this.shutdownGuard.Dispose();
            }

            await base.DisposeAsyncCore();
        }

        private void CloseAllChannels()
        {
            foreach (var (_, channel) in this.channels)
            {
                this.ClosePublisher(channel);
            }
        }

        private void LogUnconfirmedMessages()
        {
            foreach (var pending in this.pendings)
            {
                this.logger.LogError("Unconfirmed message {Message}.", pending);
            }
        }

        private async Task PublishAsync(IModel channel, IMessage @event, CancellationToken cancellationToken = default)
        {
            // Serialize event.
            var buffer = new ArrayBufferWriter<byte>();

            @event.WriteTo(buffer);

            // Setup message's properties.
            var props = channel.CreateBasicProperties();

            props.DeliveryMode = 2; // persistent
            props.Type = @event.Descriptor.FullName;
            props.ContentType = "application/x-protobuf";

            // Publish the event.
            while (true)
            {
                var pending = new PendingMessage(channel.ChannelNumber, channel.NextPublishSeqNo);

                this.AddPendingMessage(pending);

                try
                {
                    channel.BasicPublish(this.Options.Exchange, string.Empty, false, props, buffer.WrittenMemory);
                }
                catch
                {
                    this.RemovePendingMessage(pending);
                    throw;
                }

                // Wait until confirmed. We don't allowed to cancel here due to the caller don't know if the message is
                // delivered successfully or not.
                if (await pending.Completed)
                {
                    break;
                }
            }
        }

        private IModel ReserveChannel()
        {
            lock (this.channels)
            {
                if (this.channels.Count == 0)
                {
                    return this.CreatePublisher();
                }
                else
                {
                    var entry = this.channels.First();
                    this.channels.Remove(entry.Key);

                    return entry.Value;
                }
            }
        }

        private void ReleaseChannel(IModel channel)
        {
            while (true)
            {
                var time = DateTime.Now;

                lock (this.channels)
                {
                    try
                    {
                        this.channels.Add(time, channel);
                    }
                    catch (ArgumentException)
                    {
                        // Duplicated key. This case rarely happen so using exception instead of checking before
                        // inserting will have better performance due to most of the time it will lookup only once.
                        continue;
                    }
                }

                break;
            }
        }

        private void AddPendingMessage(PendingMessage pending)
        {
            lock (this.pendings)
            {
                if (!this.pendings.Add(pending))
                {
                    throw new ArgumentException($"The value {pending} is already exists.", nameof(pending));
                }
            }
        }

        private IEnumerable<PendingMessage> ListPendingMessages(PendingMessage lowest, PendingMessage highest)
        {
            lock (this.pendings)
            {
                return this.pendings.GetViewBetween(lowest, highest).ToList();
            }
        }

        private void RemovePendingMessage(PendingMessage pending)
        {
            lock (this.pendings)
            {
                if (!this.pendings.Remove(pending))
                {
                    this.logger.LogWarning("Trying to remove non-existent pending message {Message}.", pending);
                }
            }
        }

        private IModel CreatePublisher()
        {
            // FIXME: Better declare exchange only once to improve performance.
            var channel = this.CreateChannel();

            try
            {
                channel.ConfirmSelect();
                channel.BasicAcks += this.PublishConfirmed;
                channel.BasicNacks += this.PublishRejected;
            }
            catch
            {
                this.ClosePublisher(channel);
                throw;
            }

            return channel;
        }

        private void ClosePublisher(IModel channel)
        {
            channel.BasicNacks -= this.PublishRejected;
            channel.BasicAcks -= this.PublishConfirmed;

            this.CloseChannel(channel);
        }

        private void PublishConfirmed(object sender, BasicAckEventArgs e)
        {
            this.CompletePendingMessages((IModel)sender, e.DeliveryTag, e.Multiple, true);
        }

        private void PublishRejected(object sender, BasicNackEventArgs e)
        {
            this.CompletePendingMessages((IModel)sender, e.DeliveryTag, e.Multiple, false);
        }

        private void CompletePendingMessages(IModel channel, ulong last, bool multiple, bool confirmed)
        {
            // Get a list of messages.
            var highest = PendingMessage.CreateLookupKey(channel.ChannelNumber, last);
            var lowest = multiple ? PendingMessage.CreateLookupKey(channel.ChannelNumber, ulong.MinValue) : highest;
            var messages = this.ListPendingMessages(lowest, highest);
            var status = confirmed ? "Acks" : "Nacks";

            if (!messages.Any())
            {
                if (multiple)
                {
                    this.logger.LogError(
                        "Unknown multiple messages is {Status} with the highest message is {Message}.",
                        status,
                        highest);
                }
                else
                {
                    this.logger.LogError("Unknown message {Message} is {Status}.", highest, status);
                }

                return;
            }

            // Mark all pending confirmed.
            foreach (var pending in messages)
            {
                this.RemovePendingMessage(pending);

                pending.SetResult(confirmed);

                this.logger.LogInformation("Message {Message} is {Status} by the broker.", pending, status);
            }
        }
    }
}
