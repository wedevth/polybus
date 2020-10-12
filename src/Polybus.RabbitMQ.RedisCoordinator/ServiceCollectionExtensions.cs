namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Polybus.RabbitMQ;
    using Polybus.RabbitMQ.RedisCoordinator;

    public static class ServiceCollectionExtensions
    {
        public static void AddRedisQueueCoordinator(
            this IServiceCollection services,
            Action<QueueCoordinatorOptions> options)
        {
            services
                .AddOptions<QueueCoordinatorOptions>()
                .Configure(options)
                .ValidateDataAnnotations();

            services.TryAddSingleton<IQueueCoordinator, QueueCoordinator>();
        }
    }
}
