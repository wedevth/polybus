namespace Polybus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    sealed class FakeEventListener : IEventListener
    {
        public void InitializeConsumerTable(IEnumerable<IEventConsumer> consumers, Action<ConsumerDescriptor> add)
        {
            IEventListener.InitializeConsumerTable(consumers, add);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
