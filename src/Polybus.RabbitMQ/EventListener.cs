namespace Polybus.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public sealed class EventListener : EventBus, IEventListener
    {
        private readonly IConsumerIndex consumers;
        private readonly IHostApplicationLifetime host;
        private readonly ILoggerFactory loggerFactory;
        private readonly IQueueCoordinator coordinator;
        private readonly ILogger logger;
        private TaskCompletionSource<bool>? stopped;
        private IModel? channel;
        private EventReceiver? receiver;
        private volatile bool stopping;

        public EventListener(
            IOptions<EventBusOptions> options,
            IConnection connection,
            IConsumerIndex consumers,
            IHostApplicationLifetime host,
            ILoggerFactory loggerFactory,
            IQueueCoordinator coordinator)
            : base(options, connection)
        {
            this.consumers = consumers;
            this.host = host;
            this.loggerFactory = loggerFactory;
            this.coordinator = coordinator;
            this.logger = loggerFactory.CreateLogger(this.GetType());

            if (this.consumers.Count == 0)
            {
                throw new ArgumentException("No any valid consumers.", nameof(consumers));
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            // Register all supported events.
            foreach (var (@event, _) in this.consumers)
            {
                await this.coordinator.RegisterSupportedEventAsync(@event);
            }

            // Start consuming messages.
            this.stopped = new TaskCompletionSource<bool>();
            this.channel = this.CreateSubscriber();

            this.receiver = new EventReceiver(
                this.channel,
                this.coordinator,
                this.loggerFactory.CreateLogger<EventReceiver>(),
                this.consumers);
            this.receiver.ConsumerCancelled += this.ReceiverCancelled;

            // The noLocal flag is ignored:
            // https://github.com/rabbitmq/rabbitmq-java-client/issues/303#issuecomment-329469815
            this.channel.BasicConsume(this.Options.Queue, false, this.Options.Tag, false, false, null, this.receiver);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Debug.Assert(this.channel != null, $"This method required user to invoke {nameof(this.StartAsync)} first.");
            Debug.Assert(this.stopped != null, $"This method required user to invoke {nameof(this.StartAsync)} first.");

            // Stop message consumer.
            this.stopping = true;
            this.channel.BasicCancel(this.Options.Tag);

            // Wait until consumer is stopped.
            await this.stopped.Task;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeChannel();
                this.DisposeReceiver();
            }

            base.Dispose(disposing);
        }

        protected override ValueTask DisposeAsyncCore()
        {
            this.DisposeChannel();
            this.DisposeReceiver();

            return base.DisposeAsyncCore();
        }

        private void DisposeChannel()
        {
            if (this.channel != null)
            {
                this.CloseSubscriber(this.channel);
                this.channel = null;
            }
        }

        private void DisposeReceiver()
        {
            if (this.receiver != null)
            {
                this.receiver.ConsumerCancelled -= this.ReceiverCancelled;
                this.receiver = null;
            }
        }

        private Task ReceiverCancelled(object sender, ConsumerEventArgs e)
        {
            Debug.Assert(
                this.stopped != null,
                $"{nameof(this.stopped)} should be initialize before registering this event handler.");

            if (e.ConsumerTags.Length != 1 || e.ConsumerTags[0] != this.Options.Tag)
            {
                this.logger.LogWarning(
                    "Unknow message consumer {Tags} was stopped.",
                    string.Join(", ", e.ConsumerTags));
            }

            if (!this.stopping)
            {
                this.logger.LogCritical("The message consumer stopped unexpectedly.");
                this.stopped.TrySetResult(false);
                this.host.StopApplication();
            }
            else
            {
                this.stopped.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        private IModel CreateSubscriber()
        {
            // Prepare additional arguments.
            var args = new Dictionary<string, object>()
            {
                { "x-queue-type", "quorum" },
            };

            // Create a channel.
            var channel = this.CreateChannel();

            try
            {
                channel.CallbackException += this.SubscriberCallbackException;
                channel.QueueDeclare(this.Options.Queue, true, false, false, args);
                channel.QueueBind(this.Options.Queue, this.Options.Exchange, string.Empty);
            }
            catch
            {
                this.CloseSubscriber(channel);
                throw;
            }

            return channel;
        }

        private void CloseSubscriber(IModel channel)
        {
            channel.CallbackException -= this.SubscriberCallbackException;

            this.CloseChannel(channel);
        }

        private void SubscriberCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            this.logger.LogCritical(e.Exception, "Unhandled exception occurred in the channel callback.");
            this.host.StopApplication();
        }
    }
}
