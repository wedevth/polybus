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
        /// <summary>
        /// Consume a specified event and do appropriate actions.
        /// </summary>
        /// <param name="event">
        /// An event to consume.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe while waiting for consuming to complete.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="event"/> consumed successfully; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Any exceptions that throw from this method will cause event to be unsuccessful consume. Thus, it will have
        /// the same effect as returning <c>false</c>. The only different is the exception will be logged.
        /// </remarks>
        ValueTask<bool> ConsumeEventAsync(T @event, CancellationToken cancellationToken = default);
    }
}
