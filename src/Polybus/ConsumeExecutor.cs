namespace Polybus
{
    using System.Threading;
    using System.Threading.Tasks;

    public delegate ValueTask<bool> ConsumeExecutor(object @event, CancellationToken cancellationToken = default);
}
