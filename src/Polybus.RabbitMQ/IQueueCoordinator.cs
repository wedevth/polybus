namespace Polybus.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IQueueCoordinator : IAsyncDisposable, IDisposable
    {
        ValueTask RegisterSupportedEventAsync(string type, CancellationToken cancellationToken = default);

        ValueTask<bool> IsEventSupportedAsync(string type, CancellationToken cancellationToken = default);
    }
}
