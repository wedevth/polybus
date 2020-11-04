namespace Polybus.RabbitMQ.Tests
{
    using global::RabbitMQ.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Polybus.Example;
    using Xunit;

    public sealed class ServiceCollectionExtensionsTests
    {
        private readonly Mock<IHostApplicationLifetime> host;
        private readonly Mock<IQueueCoordinator> coordinator;
        private readonly Mock<ILoggerFactory> logger;
        private readonly Mock<ILogger<EventPublisher>> publisherLogger;
        private readonly ServiceCollection subject;

        public ServiceCollectionExtensionsTests()
        {
            this.host = new Mock<IHostApplicationLifetime>();
            this.coordinator = new Mock<IQueueCoordinator>();
            this.logger = new Mock<ILoggerFactory>();
            this.publisherLogger = new Mock<ILogger<EventPublisher>>();
            this.subject = new ServiceCollection();

            this.subject.AddSingleton(this.host.Object);
            this.subject.AddSingleton(this.coordinator.Object);
            this.subject.AddSingleton(this.logger.Object);
            this.subject.AddSingleton(this.publisherLogger.Object);
        }

        [Fact]
        public void AddRabbitMQPublisher_WhenInvoked_ShouldResolveIEventPublisherSuccessfully()
        {
            // Arrange.
            this.subject.AddRabbitMQConnection(new ConnectionFactory()
            {
                DispatchConsumersAsync = true,
            });

            this.subject.ConfigureRabbitMQEventBus(options =>
            {
                options.Exchange = "abc";
                options.Queue = "def";
                options.Tag = "ghi";
            });

            // Act.
            this.subject.AddRabbitMQPublisher();

            // Assert.
            using var provider = this.subject.BuildServiceProvider();
            var result = provider.GetService<IEventPublisher>();

            Assert.NotNull(result);
        }

        [Fact]
        public void AddRabbitMQSubscriber_WhenInvoked_ShouldResolveEventListenerSuccessfully()
        {
            // Arrange.
            var consumer = new Mock<IEventConsumer<Person>>();

            this.subject.AddRabbitMQConnection(new ConnectionFactory()
            {
                DispatchConsumersAsync = true,
            });

            this.subject.ConfigureRabbitMQEventBus(options =>
            {
                options.Exchange = "abc";
                options.Queue = "def";
                options.Tag = "ghi";
            });

            this.subject.AddEventConsumer(consumer.Object);

            // Act.
            this.subject.AddRabbitMQSubscriber();

            // Assert.
            using var provider = this.subject.BuildServiceProvider();
            var result = provider.GetServices<IHostedService>();

            Assert.Contains(result, h => h.GetType() == typeof(EventListener));
        }
    }
}
