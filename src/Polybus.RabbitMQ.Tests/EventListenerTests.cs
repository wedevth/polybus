namespace Polybus.RabbitMQ.Tests
{
    using System;
    using System.Buffers;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Polybus.Example;
    using Xunit;
    using static Polybus.Example.Person.Types;

    public sealed class EventListenerTests : RabbitMQTests
    {
        private readonly IModel publisher;
        private readonly Mock<IHostApplicationLifetime> host;
        private readonly Mock<ILogger> logger;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<IQueueCoordinator> coordinator;
        private readonly Mock<IEventConsumer<Person>> consumer;
        private readonly EventListener subject;

        public EventListenerTests()
        {
            try
            {
                this.publisher = this.CreatePublisher();
                this.publisher.QueueDelete(this.Options.Queue, false, false);

                this.host = new Mock<IHostApplicationLifetime>();
                this.logger = new Mock<ILogger>();

                this.loggerFactory = new Mock<ILoggerFactory>();
                this.loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(this.logger.Object);

                this.coordinator = new Mock<IQueueCoordinator>();
                this.consumer = new Mock<IEventConsumer<Person>>();
                this.subject = new EventListener(
                    new OptionsWrapper<EventBusOptions>(this.Options),
                    this.Connection,
                    this.host.Object,
                    this.loggerFactory.Object,
                    this.coordinator.Object,
                    new[] { this.consumer.Object });
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
                if (this.publisher != null)
                {
                    this.publisher.Close();
                    this.publisher.Dispose();
                }

                this.subject?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void PublishEvent(IMessage @event)
        {
            var buffer = new ArrayBufferWriter<byte>();
            @event.WriteTo(buffer);

            var props = this.publisher.CreateBasicProperties();

            props.DeliveryMode = 2;
            props.Type = @event.Descriptor.FullName;
            props.ContentType = "application/x-protobuf";

            this.publisher.BasicPublish(this.Options.Exchange, string.Empty, false, props, buffer.WrittenMemory);
        }

        private IModel CreatePublisher()
        {
            var channel = this.Connection.CreateModel();

            try
            {
                channel.ExchangeDeclare(this.Options.Exchange, ExchangeType.Fanout, true, false);
            }
            catch
            {
                channel.Close();
                channel.Dispose();
                throw;
            }

            return channel;
        }

        [Fact]
        public async Task StartAsync_WhenReceivedValidEvent_ShouldInvokeCorrespondingConsumer()
        {
            // Arrange.
            using var received = new SemaphoreSlim(0);
            var @event = new Person()
            {
                Id = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                Name = "John Doe",
                LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            @event.Emails.Add("jd@example.com");
            @event.Phones.Add(new PhoneNumber()
            {
                Number = "+66123456789",
                Type = PhoneType.Home,
            });

            this.consumer
                .Setup(c => c.ConsumeEventAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
                .Returns<Person, CancellationToken>((p, c) =>
                {
                    received.Release();
                    return new ValueTask(Task.CompletedTask);
                });

            // Act.
            bool result;

            await this.subject.StartAsync();

            try
            {
                this.PublishEvent(@event);

                result = await received.WaitAsync(1000 * 5);
            }
            finally
            {
                await this.subject.StopAsync();
            }

            // Assert.
            Assert.True(result);

            this.coordinator.Verify(
                c => c.RegisterSupportedEventAsync(Person.Descriptor.FullName, default),
                Times.Once());

            this.coordinator.Verify(
                c => c.RegisterSupportedEventAsync(
                    It.IsNotIn(Person.Descriptor.FullName),
                    It.IsAny<CancellationToken>()),
                Times.Never());

            this.coordinator.Verify(
                c => c.IsEventSupportedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());

            this.consumer.Verify(
                c => c.ConsumeEventAsync(@event, default),
                Times.Once());

            this.consumer.Verify(
                c => c.ConsumeEventAsync(It.IsNotIn(@event), It.IsAny<CancellationToken>()),
                Times.Never());

            this.host.Verify(
                h => h.StopApplication(),
                Times.Never());

            this.logger.Verify(
                l => l.Log(
                    It.IsNotIn(LogLevel.Information),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Never());
        }

        [Fact(Skip = "There is a bug in rabbitmq-dotnet-client: https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/946")]
        public async Task StartAsync_ExceptionInChannelCallback_ShouldStopApplication()
        {
            // Arrange.
            using var stopped = new SemaphoreSlim(0);
            var @event = new AddressBook();
            var ex = new Exception();

            this.coordinator
                .Setup(c => c.IsEventSupportedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);

            this.host
                .Setup(h => h.StopApplication())
                .Callback(() => stopped.Release());

            // Act.
            bool result;

            await this.subject.StartAsync();

            try
            {
                this.PublishEvent(@event);

                result = await stopped.WaitAsync(1000 * 5);
            }
            finally
            {
                await this.subject.StopAsync();
            }

            // Assert.
            Assert.True(result);

            this.coordinator.Verify(
                c => c.RegisterSupportedEventAsync(Person.Descriptor.FullName, default),
                Times.Once());

            this.coordinator.Verify(
                c => c.RegisterSupportedEventAsync(
                    It.IsNotIn(Person.Descriptor.FullName),
                    It.IsAny<CancellationToken>()),
                Times.Never());

            this.coordinator.Verify(
                c => c.IsEventSupportedAsync(AddressBook.Descriptor.FullName, default),
                Times.Once());

            this.coordinator.Verify(
                c => c.IsEventSupportedAsync(
                    It.IsNotIn(AddressBook.Descriptor.FullName),
                    It.IsAny<CancellationToken>()),
                Times.Never());

            this.consumer.Verify(
                c => c.ConsumeEventAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()),
                Times.Never());

            this.host.Verify(
                h => h.StopApplication(),
                Times.Once());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Critical,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == "Unhandled exception occurred in the channel callback."),
                    ex,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());
        }
    }
}
