namespace Polybus.RabbitMQ.Tests
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Google.Protobuf;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Polybus.Example;
    using Xunit;

    public sealed class EventPublisherTests : RabbitMQTests
    {
        private const int PersonId = 211;

        private readonly ManualResetEventSlim received;
        private readonly Mock<ILogger<EventPublisher>> logger;
        private readonly IModel consumer;
        private readonly EventPublisher subject;
        private CountdownEvent? persons;
        private CountdownEvent? addressBooks;

        public EventPublisherTests()
        {
            try
            {
                this.received = new ManualResetEventSlim();
                this.logger = new Mock<ILogger<EventPublisher>>();

                this.consumer = this.CreateConsumer();
                this.subject = new EventPublisher(
                    new OptionsWrapper<EventBusOptions>(this.Options),
                    this.Connection,
                    this.logger.Object);
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.subject?.Dispose();

                if (this.consumer != null)
                {
                    this.CloseConsumer(this.consumer);
                }

                this.addressBooks?.Dispose();
                this.persons?.Dispose();
                this.received?.Dispose();
            }

            base.Dispose(disposing);
        }

        [Fact]
        public async Task Dispose_InvokeWhileOthersBeingPublishing_ShouldStopPublishingCorrectly()
        {
            // Arrange.
            using var canceler = new CancellationTokenSource();
            var backgrounds = new List<Task>();

            for (var i = 0; i < 4; i++)
            {
                backgrounds.Add(this.PublishLoop(() => true, canceler.Token));
            }

            Assert.True(this.received.Wait(5000));

            // Act.
            this.subject.Dispose();

            // Assert.
            foreach (var background in backgrounds)
            {
                var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => background);

                Assert.Equal(typeof(EventPublisher).FullName, ex.ObjectName);
            }

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Never());
        }

        [Fact]
        public async Task DisposeAsync_InvokeWhileOthersBeingPublishing_ShouldStopPublishingCorrectly()
        {
            // Arrange.
            using var canceler = new CancellationTokenSource();
            var backgrounds = new List<Task>();

            for (var i = 0; i < 4; i++)
            {
                backgrounds.Add(this.PublishLoop(() => true, canceler.Token));
            }

            Assert.True(this.received.Wait(5000));

            // Act.
            await this.subject.DisposeAsync();

            // Assert.
            foreach (var background in backgrounds)
            {
                var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => background);

                Assert.Equal(typeof(EventPublisher).FullName, ex.ObjectName);
            }

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Never());
        }

        [Fact]
        public async Task PublishAsync_WhenInvokeConcurrently_ShouldPublishEventCorrectly()
        {
            // Arrange.
            using var canceler = new CancellationTokenSource();
            var backgrounds = new List<Task>();
            var backgroundTarget = 400;

            this.persons = new CountdownEvent(100);
            this.addressBooks = new CountdownEvent(400);

            for (var i = 0; i < 4; i++)
            {
                var background = this.PublishLoop(
                    () => Interlocked.Decrement(ref backgroundTarget) >= 0,
                    canceler.Token);

                backgrounds.Add(background);
            }

            try
            {
                // Act.
                for (var i = 0; i < 100; i++)
                {
                    var @event = new Person()
                    {
                        Id = PersonId,
                    };

                    await this.subject.PublishAsync(@event);
                }

                // Assert.
                Assert.True(this.persons.Wait(1000 * 10));
                Assert.True(this.addressBooks.Wait(1000 * 10));
            }
            finally
            {
                canceler.Cancel();

                try
                {
                    await Task.WhenAll(backgrounds);
                }
                catch (TaskCanceledException)
                {
                    // Ignore.
                }
            }

            await this.subject.DisposeAsync();

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => $"{v}".EndsWith(" is Acks by the broker.")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Exactly(500));

            this.logger.Verify(
                l => l.Log(
                    It.IsIn(LogLevel.Warning, LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Never());
        }

        private async Task PublishLoop(Func<bool> @continue, CancellationToken cancellationToken)
        {
            await Task.Yield();

            while (@continue())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var @event = new AddressBook();

                @event.People.Add(new Person()
                {
                    Id = PersonId,
                });

                await this.subject.PublishAsync(@event, cancellationToken);
            }
        }

        private Task Received(object sender, BasicDeliverEventArgs e)
        {
            var type = e.BasicProperties.Type;
            var body = new ReadOnlySequence<byte>(e.Body);

            this.received.Set();

            if (type == Person.Descriptor.FullName)
            {
                var @event = Person.Parser.ParseFrom(body);

                if (@event.Id == PersonId)
                {
                    this.persons?.Signal();
                }
            }
            else if (type == AddressBook.Descriptor.FullName)
            {
                var @event = AddressBook.Parser.ParseFrom(body);

                if (@event.People.Count == 1 && @event.People[0].Id == PersonId)
                {
                    this.addressBooks?.Signal();
                }
            }

            return Task.CompletedTask;
        }

        private IModel CreateConsumer()
        {
            var channel = this.Connection.CreateModel();
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += this.Received;

            channel.ExchangeDeclare(this.Options.Exchange, ExchangeType.Fanout, true, false);
            channel.QueueDeclare(this.Options.Queue, true, false, false, new Dictionary<string, object>()
            {
                { "x-queue-type", "quorum" },
            });
            channel.QueuePurge(this.Options.Queue);
            channel.QueueBind(this.Options.Queue, this.Options.Exchange, string.Empty);
            channel.BasicConsume(this.Options.Queue, true, this.GetType().FullName, false, false, null, consumer);

            return channel;
        }

        private void CloseConsumer(IModel channel)
        {
            channel.Close();
            channel.Dispose();
        }
    }
}
