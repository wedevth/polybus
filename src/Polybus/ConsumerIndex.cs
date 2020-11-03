namespace Polybus
{
    using System.Collections;
    using System.Collections.Generic;

    public sealed class ConsumerIndex : IConsumerIndex
    {
        private readonly Dictionary<string, ConsumerDescriptor> index;

        public ConsumerIndex(IEnumerable<IEventConsumer> consumers)
        {
            this.index = new Dictionary<string, ConsumerDescriptor>();

            IConsumerIndex.InitializeIndex(consumers, descriptor =>
            {
                 this.index.Add(descriptor.EventDescriptor.FullName, descriptor);
            });
        }

        public IEnumerable<string> Keys => this.index.Keys;

        public IEnumerable<ConsumerDescriptor> Values => this.index.Values;

        public int Count => this.index.Count;

        public ConsumerDescriptor this[string key] => this.index[key];

        public bool ContainsKey(string key) => this.index.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, ConsumerDescriptor>> GetEnumerator() => this.index.GetEnumerator();

        public bool TryGetValue(string key, out ConsumerDescriptor value) => this.index.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.index).GetEnumerator();
    }
}
