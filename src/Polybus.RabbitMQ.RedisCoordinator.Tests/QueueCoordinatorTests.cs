namespace Polybus.RabbitMQ.RedisCoordinator.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using StackExchange.Redis;
    using Xunit;

    public sealed class QueueCoordinatorTests : IDisposable
    {
        private readonly QueueCoordinatorOptions options;
        private readonly ConnectionMultiplexer redis;
        private readonly QueueCoordinator subject;

        public QueueCoordinatorTests()
        {
            this.options = new QueueCoordinatorOptions()
            {
                KeyPrefix = "wedev.polybus.rabbit.redis-coordinator-test",
            };

            this.redis = ConnectionMultiplexer.Connect("localhost,allowAdmin=true");

            try
            {
                foreach (var endpoint in this.redis.GetEndPoints())
                {
                    var server = this.redis.GetServer(endpoint);

                    server.ConfigSet("notify-keyspace-events", "EA");
                }

                this.subject = new QueueCoordinator(
                    new OptionsWrapper<QueueCoordinatorOptions>(this.options),
                    this.redis);
            }
            catch
            {
                this.redis.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            this.subject.Dispose();
            this.redis.Dispose();
        }

        private async Task ClearRedisKeys()
        {
            foreach (var endpoint in this.redis.GetEndPoints())
            {
                var server = this.redis.GetServer(endpoint);

                await server.FlushAllDatabasesAsync();
            }
        }

        private async Task UpdateNodeTimestampAsync(string eventType, string node, long timestamp)
        {
            var db = this.redis.GetDatabase();
            var key = $"{this.options.KeyPrefix}:{eventType}";

            await db.SortedSetAddAsync(key, node, timestamp);
        }

        [Fact]
        public async Task IsEventSupportedAsync_WithNoRegisteredEvent_ShouldReturnFalse()
        {
            await this.ClearRedisKeys();

            var result = await this.subject.IsEventSupportedAsync("abc");

            Assert.False(result);
        }

        [Fact]
        public async Task IsEventSupportedAsync_WithStallNodes_ShouldReturnFalse()
        {
            // Arrange.
            var event1 = "abc";
            var event2 = "def";
            var now = DateTimeOffset.UtcNow;
            var threshold = this.options.StallThreshold;

            await this.ClearRedisKeys();

            await this.UpdateNodeTimestampAsync(event1, "node1", (now - threshold).ToUnixTimeSeconds() - 1);
            await this.UpdateNodeTimestampAsync(event1, "node2", (now - threshold).ToUnixTimeSeconds() - 60);
            await this.UpdateNodeTimestampAsync(event2, "node1", now.ToUnixTimeSeconds());

            // Act.
            var result = await this.subject.IsEventSupportedAsync(event1);

            // Assert.
            Assert.False(result);
        }

        [Fact]
        public async Task IsEventSupportedAsync_WithNonStallNodes_ShouldReturnTrue()
        {
            // Arrange.
            var event1 = "abc";
            var event2 = "def";
            var now = DateTimeOffset.UtcNow;
            var threshold = this.options.StallThreshold;

            await this.ClearRedisKeys();

            await this.UpdateNodeTimestampAsync(event1, "node1", (now - threshold).ToUnixTimeSeconds() - 1);
            await this.UpdateNodeTimestampAsync(event1, "node2", (now - threshold).ToUnixTimeSeconds() - 60);
            await this.UpdateNodeTimestampAsync(event2, "node1", now.ToUnixTimeSeconds());

            // Act.
            var result = await this.subject.IsEventSupportedAsync(event2);

            // Assert.
            Assert.True(result);
        }

        [Fact]
        public async Task RegisterSupportedEventAsync_WhenInvoke_ShouldUpdateNodeTimestamp()
        {
            // Arrange.
            using var registered = new SemaphoreSlim(0);
            var @event = "abc";
            var key = $"{this.options.KeyPrefix}:{@event}";
            var subscriber = this.redis.GetSubscriber();

            await subscriber.SubscribeAsync("__keyevent@0__:zadd", (channel, message) =>
            {
                if (message == key)
                {
                    registered.Release();
                }
            });

            // Act.
            await this.subject.RegisterSupportedEventAsync(@event);

            // Assert.
            Assert.True(await registered.WaitAsync(1000 * 5));

            var db = this.redis.GetDatabase();
            var score = await db.SortedSetScoreAsync(key, Environment.MachineName);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Assert.True(score.HasValue);
            Assert.InRange(score.Value, now - 5, now);
        }
    }
}
