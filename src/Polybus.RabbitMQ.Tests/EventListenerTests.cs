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
        private readonly Mock<IEventConsumer<Person>> consumer1;
        private readonly Mock<IEventConsumer<AddressBook>> consumer2;
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
                this.consumer1 = new Mock<IEventConsumer<Person>>();
                this.consumer2 = this.consumer1.As<IEventConsumer<AddressBook>>();
                this.subject = new EventListener(
                    new OptionsWrapper<EventBusOptions>(this.Options),
                    this.Connection,
                    new ConsumerIndex(new[] { this.consumer1.Object }),
                    this.host.Object,
                    this.loggerFactory.Object,
                    this.coordinator.Object);
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
            using var remaining = new CountdownEvent(2);
            var ex = new NotSupportedException();
            var event1 = new Person()
            {
                Id = 1,
                Name = "John Doe",
                LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            event1.Emails.Add("jd@example.com");
            event1.Phones.Add(new PhoneNumber()
            {
                Number = "+66123456789",
                Type = PhoneType.Home,
            });

            var event2 = new AddressBook();

            event2.People.Add(event1);

            this.consumer1
                .SetupSequence(c => c.ConsumeEventAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex)
                .ReturnsAsync(() =>
                {
                    remaining.Signal();
                    return true;
                });

            this.consumer2
                .SetupSequence(c => c.ConsumeEventAsync(It.IsAny<AddressBook>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false)
                .ReturnsAsync(() =>
                {
                    remaining.Signal();
                    return true;
                });

            // Act.
            bool result;

            await this.subject.StartAsync();

            try
            {
                this.PublishEvent(event1);
                this.PublishEvent(event2);

                result = remaining.Wait(1000 * 5);
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
                c => c.RegisterSupportedEventAsync(AddressBook.Descriptor.FullName, default),
                Times.Once());

            this.coordinator.Verify(
                c => c.IsEventSupportedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());

            this.consumer1.Verify(
                c => c.ConsumeEventAsync(event1, default),
                Times.Exactly(2));

            this.consumer2.Verify(
                c => c.ConsumeEventAsync(event2, default),
                Times.Exactly(2));

            this.host.Verify(
                h => h.StopApplication(),
                Times.Never());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Consuming {Person.Descriptor.FullName}: {event1}"),
                    null,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Exactly(2));

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Consuming {AddressBook.Descriptor.FullName}: {event2}"),
                    null,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Exactly(2));

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Unhandled exception occurred while consuming {Person.Descriptor.FullName}."),
                    ex,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Unsuccessful consuming {Person.Descriptor.FullName}, returning event to the queue to let other instance handle it."),
                    null,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Unsuccessful consuming {AddressBook.Descriptor.FullName}, returning event to the queue to let other instance handle it."),
                    null,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Successful consuming {Person.Descriptor.FullName}."),
                    null,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == $"Successful consuming {AddressBook.Descriptor.FullName}."),
                    null,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());
        }

        [Fact]
        public async Task StartAsync_ExceptionInChannelCallback_ShouldStopApplication()
        {
            // Arrange.
            using var stopped = new SemaphoreSlim(0);
            var @event = new StreetAddress();
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
                c => c.RegisterSupportedEventAsync(StreetAddress.Descriptor.FullName, It.IsAny<CancellationToken>()),
                Times.Never());

            this.coordinator.Verify(
                c => c.IsEventSupportedAsync(StreetAddress.Descriptor.FullName, default),
                Times.Once());

            this.host.Verify(
                h => h.StopApplication(),
                Times.Once());

            this.logger.Verify(
                l => l.Log(
                    LogLevel.Critical,
                    0,
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == "Unhandled exception occurred in the channel callback."),
                    ex,
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once());
        }
    }
}
