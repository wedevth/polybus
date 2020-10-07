namespace Polybus.RabbitMQ
{
    using System.ComponentModel.DataAnnotations;

    public sealed class EventBusOptions
    {
        /// <summary>
        /// Gets or sets name of the exchange to connect to.
        /// </summary>
        /// <remarks>
        /// This value must be the same for all services.
        /// </remarks>
        [Required]
        [Name]
        public string Exchange { get; set; } = null!;

        /// <summary>
        /// Gets or sets name of the queue to subscribe for events.
        /// </summary>
        /// <remarks>
        /// This value must be unique for each service. Usually it will be name of the service.
        /// </remarks>
        [Required]
        [Name]
        public string Queue { get; set; } = null!;

        /// <summary>
        /// Gets or sets name of the consumer tag.
        /// </summary>
        /// <remarks>
        /// Usually this will be name of the service.
        /// </remarks>
        [Required]
        [Name]
        public string Tag { get; set; } = null!;
    }
}
