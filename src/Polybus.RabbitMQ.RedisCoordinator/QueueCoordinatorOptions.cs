namespace Polybus.RabbitMQ.RedisCoordinator
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public sealed class QueueCoordinatorOptions
    {
        /// <summary>
        /// Gets or sets the prefix of all keys to store on Redis.
        /// </summary>
        /// <remarks>
        /// This value must be the same for all instances of the same service.
        /// </remarks>
        [Required]
        public string KeyPrefix { get; set; } = null!;

        /// <summary>
        /// Gets or sets the name of instance of the current service.
        /// </summary>
        /// <remarks>
        /// This value must be unique for each service instance and must not changed when service is restarted. Set this
        /// value to <c>null</c> to use current hostname instead.
        /// </remarks>
        public string? NodeName { get; set; }

        /// <summary>
        /// Gets or sets the threshold to consider node is stalled.
        /// </summary>
        /// <remarks>
        /// The default value is 5 minutes.
        /// </remarks>
        public TimeSpan StallThreshold { get; set; } = new TimeSpan(0, 5, 0);

        /// <summary>
        /// Gets or sets the invertal to update node's timestamp.
        /// </summary>
        /// <remarks>
        /// The default value is 3 minutes.
        /// </remarks>
        public TimeSpan TimestampUpdateInterval { get; set; } = new TimeSpan(0, 3, 0);
    }
}
