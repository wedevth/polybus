namespace Polybus
{
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Protobuf;

    public interface IEventPublisher
    {
        ValueTask PublishAsync(IMessage @event, CancellationToken cancellationToken = default);
    }
}
