namespace Polybus.RabbitMQ
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Microsoft.Extensions.Logging;

    internal sealed class EventReceiver : AsyncDefaultBasicConsumer
    {
        private readonly IQueueCoordinator coordinator;
        private readonly ILogger logger;
        private readonly IReadOnlyDictionary<string, ConsumerDescriptor> consumers;

        public EventReceiver(
            IModel channel,
            IQueueCoordinator coordinator,
            ILogger<EventReceiver> logger,
            IReadOnlyDictionary<string, ConsumerDescriptor> consumers)
            : base(channel)
        {
            this.coordinator = coordinator;
            this.logger = logger;
            this.consumers = consumers;
        }

        public override async Task HandleBasicDeliver(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            // FIXME: This is a workaround until this PR is released:
            // https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/946
            await Task.Yield();

            // Sanity checks.
            if (!properties.IsTypePresent() || !properties.IsContentTypePresent())
            {
                this.logger.LogWarning(
                    "Found an unknow message {DeliveryTag} from {Exchange}.",
                    deliveryTag,
                    exchange);
                this.Model.BasicReject(deliveryTag, false);
                return;
            }

            var eventType = properties.Type;
            var contentType = properties.ContentType;

            if (!string.Equals(contentType, "application/x-protobuf", StringComparison.OrdinalIgnoreCase))
            {
                // We want to requeue because the other instance with newer version may supports this new content type.
                this.Model.BasicReject(deliveryTag, true);
                return;
            }

            // Find a consumer to handler.
            if (!this.consumers.TryGetValue(eventType, out var consumer))
            {
                // We need to requeue if other instance can handle this event. This can happen if the newer version of
                // the service is deployed alongside old version.
                var requeue = await this.coordinator.IsEventSupportedAsync(eventType);
                this.Model.BasicReject(deliveryTag, requeue);
                return;
            }

            // Invoke consumer.
            var @event = consumer.EventParser.ParseFrom(new ReadOnlySequence<byte>(body));

            try
            {
                await consumer.ConsumeExecutor(@event);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Unhandled exception occurred while consuming event {EventType}.", eventType);

                // Requeue to let the other instance handle this event instead.
                this.Model.BasicReject(deliveryTag, true);
                return;
            }

            this.Model.BasicAck(deliveryTag, false);
        }
    }
}
