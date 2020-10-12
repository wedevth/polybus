namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Polybus;
    using Polybus.RabbitMQ;
    using RabbitMQ.Client;

    public static class ServiceCollectionExtensions
    {
        public static void ConfigureRabbitMQEventBus(this IServiceCollection services, Action<EventBusOptions> options)
        {
            services
                .AddOptions<EventBusOptions>()
                .Configure(options)
                .ValidateDataAnnotations();
        }

        public static void AddRabbitMQConnection(this IServiceCollection services, IAsyncConnectionFactory factory)
        {
            if (!factory.DispatchConsumersAsync)
            {
                throw new ArgumentException("The factory required to enabled DispatchConsumersAsync.", nameof(factory));
            }

            // FIXME: Find a solution to close the connection when disposing.
            services.TryAddSingleton<IConnection>(p => factory.CreateConnection());
        }

        public static void AddRabbitMQPublisher(this IServiceCollection services)
        {
            services.TryAddSingleton<IEventPublisher, EventPublisher>();
        }

        public static void AddRabbitMQsubscriber(this IServiceCollection services)
        {
            services.AddHostedService<EventListener>();
        }
    }
}
