namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using System.Linq;
    using Polybus;

    public static class ServiceCollectionExtensions
    {
        public static void AddEventConsumer<T>(this IServiceCollection services)
            where T : class, IEventConsumer
        {
            var consumer = typeof(T);

            if (!IsValidConsumer(typeof(T)))
            {
                throw new InvalidOperationException($"{consumer} must also implement {typeof(IEventConsumer<>)}.");
            }

            services.AddSingleton<IEventConsumer, T>();
        }

        private static bool IsValidConsumer(Type consumer)
        {
            return consumer
                .GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventConsumer<>));
        }
    }
}
