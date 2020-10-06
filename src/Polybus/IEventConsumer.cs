namespace Polybus
{
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Protobuf;

    public interface IEventConsumer
    {
    }

    public interface IEventConsumer<in T> : IEventConsumer
        where T : IMessage
    {
        ValueTask ConsumeEventAsync(T @event, CancellationToken cancellationToken = default);
    }
}
