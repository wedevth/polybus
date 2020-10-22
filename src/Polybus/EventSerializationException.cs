namespace Polybus
{
    using System;
    using System.Runtime.Serialization;

    public class EventSerializationException : Exception
    {
        public EventSerializationException()
        {
        }

        public EventSerializationException(string message)
            : base(message)
        {
        }

        public EventSerializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected EventSerializationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
