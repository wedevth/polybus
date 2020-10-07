namespace Polybus.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Microsoft.Extensions.Options;

    public abstract class EventBus : IAsyncDisposable, IDisposable
    {
        protected EventBus(IOptions<EventBusOptions> options, IConnection connection)
        {
            this.Options = options.Value;
            this.Connection = connection;
        }

        protected EventBusOptions Options { get; }

        protected IConnection Connection { get; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await this.DisposeAsyncCore();
            this.Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            return new ValueTask(Task.CompletedTask);
        }

        protected IModel CreateChannel(bool declareExchange = true)
        {
            var channel = this.Connection.CreateModel();

            try
            {
                if (declareExchange)
                {
                    channel.ExchangeDeclare(this.Options.Exchange, ExchangeType.Fanout, true, false);
                }
            }
            catch
            {
                this.CloseChannel(channel);
                throw;
            }

            return channel;
        }

        protected void CloseChannel(IModel channel)
        {
            // The channel needs to close explicitly as stated here:
            // https://www.rabbitmq.com/dotnet-api-guide.html#disconnecting
            channel.Close();
            channel.Dispose();
        }
    }
}
