namespace Polybus.RabbitMQ
{
    using System;
    using System.Threading.Tasks;

    public sealed class PendingMessage : IComparable<PendingMessage>
    {
        private readonly TaskCompletionSource<bool>? completed;

        public PendingMessage(int channel, ulong deliveryTag)
            : this(channel, deliveryTag, new TaskCompletionSource<bool>())
        {
        }

        private PendingMessage(int channel, ulong deliveryTag, TaskCompletionSource<bool>? completed)
        {
            this.Channel = channel;
            this.DeliveryTag = deliveryTag;
            this.completed = completed;
        }

        public int Channel { get; }

        public ulong DeliveryTag { get; }

        public Task<bool> Completed
        {
            get
            {
                return this.completed?.Task ?? throw new InvalidOperationException("Incomplete object.");
            }
        }

        public static PendingMessage CreateLookupKey(int channel, ulong deliveryTag)
        {
            return new PendingMessage(channel, deliveryTag, null);
        }

        public int CompareTo(PendingMessage? other)
        {
            if (other == null || this.Channel > other.Channel)
            {
                return 1;
            }
            else if (this.Channel < other.Channel)
            {
                return -1;
            }
            else if (this.DeliveryTag > other.DeliveryTag)
            {
                return 1;
            }
            else if (this.DeliveryTag < other.DeliveryTag)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        public void SetResult(bool confirmed)
        {
            if (this.completed == null)
            {
                throw new InvalidOperationException("Incomplete object.");
            }

            this.completed.SetResult(confirmed);
        }

        public override string ToString()
        {
            return $"{this.Channel}:{this.DeliveryTag}";
        }
    }
}
