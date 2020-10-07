namespace Polybus.RabbitMQ
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NameAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null)
            {
                return true;
            }

            // https://www.rabbitmq.com/queues.html#names
            return Encoding.UTF8.GetByteCount((string)value) <= 255;
        }
    }
}
