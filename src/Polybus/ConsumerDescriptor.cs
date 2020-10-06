namespace Polybus
{
    using Google.Protobuf;
    using Google.Protobuf.Reflection;

    public sealed class ConsumerDescriptor
    {
        public ConsumerDescriptor(
            IEventConsumer instance,
            MessageDescriptor descriptor,
            MessageParser parser,
            ConsumeExecutor executor)
        {
            this.Instance = instance;
            this.EventDescriptor = descriptor;
            this.EventParser = parser;
            this.ConsumeExecutor = executor;
        }

        public IEventConsumer Instance { get; }

        public MessageDescriptor EventDescriptor { get; }

        public MessageParser EventParser { get; }

        public ConsumeExecutor ConsumeExecutor { get; }
    }
}
