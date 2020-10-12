namespace Polybus.RabbitMQ.RedisCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using StackExchange.Redis;

    public sealed class QueueCoordinator : IQueueCoordinator
    {
        private readonly QueueCoordinatorOptions options;
        private readonly IConnectionMultiplexer redis;
        private readonly List<Timer> timers;
        private bool disposed;

        public QueueCoordinator(IOptions<QueueCoordinatorOptions> options, IConnectionMultiplexer redis)
        {
            this.options = options.Value;
            this.redis = redis;
            this.timers = new List<Timer>();
        }

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

        public async ValueTask<bool> IsEventSupportedAsync(string type, CancellationToken cancellationToken = default)
        {
            var db = this.redis.GetDatabase();
            var key = this.GetEventKey(type);
            var threshold = (DateTimeOffset.UtcNow - this.options.StallThreshold).ToUnixTimeSeconds();

            return (await db.SortedSetLengthAsync(key, threshold)) > 0;
        }

        public async ValueTask RegisterSupportedEventAsync(string type, CancellationToken cancellationToken = default)
        {
            var timer = new Timer(this.UpdateNodeTimestamp, type, TimeSpan.Zero, this.options.TimestampUpdateInterval);

            try
            {
                lock (this.timers)
                {
                    this.timers.Add(timer);
                }
            }
            catch
            {
                await timer.DisposeAsync();
                throw;
            }
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    lock (this.timers)
                    {
                        foreach (var timer in this.timers)
                        {
                            using var stopped = new ManualResetEvent(false);

                            if (timer.Dispose(stopped))
                            {
                                stopped.WaitOne();
                            }
                        }

                        this.timers.Clear();
                    }
                }

                this.disposed = true;
            }
        }

        private async Task DisposeAsyncCore()
        {
            if (!this.disposed)
            {
                IEnumerable<Timer> timers;

                lock (this.timers)
                {
                    timers = this.timers.ToArray();
                    this.timers.Clear();
                }

                foreach (var timer in timers)
                {
                    await timer.DisposeAsync();
                }
            }
        }

        private void UpdateNodeTimestamp(object state)
        {
            var eventType = (string)state;
            var db = this.redis.GetDatabase();
            var key = this.GetEventKey(eventType);
            var member = this.options.NodeName ?? Environment.MachineName;
            var score = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            db.SortedSetAdd(key, member, score, CommandFlags.FireAndForget);
        }

        private RedisKey GetEventKey(string type)
        {
            return $"{this.options.KeyPrefix}:{type}";
        }
    }
}
