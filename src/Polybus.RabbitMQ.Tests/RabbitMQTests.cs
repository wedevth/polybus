namespace Polybus.RabbitMQ.Tests
{
    using System;
    using global::RabbitMQ.Client;
    using Xunit;

    [Collection("RabbitMQ interfacing tests")]
    public abstract class RabbitMQTests : IDisposable
    {
        protected RabbitMQTests()
        {
            var factory = new ConnectionFactory()
            {
                DispatchConsumersAsync = true,
            };

            this.Options = new EventBusOptions()
            {
                Exchange = "polybus.test",
                Queue = "polybus.test",
                Tag = "polybus.test-consumer",
            };

            this.Connection = factory.CreateConnection();
        }

        protected EventBusOptions Options { get; }

        protected IConnection Connection { get; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Connection.Close();
                this.Connection.Dispose();
            }
        }
    }
}
