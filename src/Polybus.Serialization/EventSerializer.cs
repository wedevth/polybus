namespace Polybus.Serialization
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using Google.Protobuf;

    public static class EventSerializer
    {
        /// <summary>
        /// Deserialize <see cref="ByteString"/> that contains a variant 1 UUID to <see cref="Guid"/>.
        /// </summary>
        /// <param name="data">
        /// Raw data of variant 1 UUID.
        /// </param>
        /// <returns>
        /// The native <see cref="Guid"/> that represents the same value as <paramref name="data"/>.
        /// </returns>
        /// <exception cref="EventSerializationException">
        /// <paramref name="data"/> contains invalid data.
        /// </exception>
        /// <remarks>
        /// This method produce a different result from <c>new Guid(data.Span)</c>, which is not correct due to
        /// incompatible endianness.
        /// </remarks>
        public static Guid DeserializeGuid(ByteString data)
        {
            if (data.Length != 16)
            {
                throw new EventSerializationException("The data must be exactly 16 bytes.");
            }

            if (!IsVariant1UUID(data.Span))
            {
                throw new EventSerializationException("The data must be variant 1 UUID.");
            }

            // Convert some fields from big-endian to little-endian.
            using var buffer = MemoryPool<byte>.Shared.Rent(16);
            var output = buffer.Memory.Span;

            var a = BinaryPrimitives.ReadInt32BigEndian(data.Span.Slice(0));
            var b = BinaryPrimitives.ReadInt16BigEndian(data.Span.Slice(4));
            var c = BinaryPrimitives.ReadInt16BigEndian(data.Span.Slice(6));

            BinaryPrimitives.WriteInt32LittleEndian(output.Slice(0), a);
            BinaryPrimitives.WriteInt16LittleEndian(output.Slice(4), b);
            BinaryPrimitives.WriteInt16LittleEndian(output.Slice(6), c);
            data.Span.Slice(8).CopyTo(output.Slice(8));

            return new Guid(output.Slice(0, 16));
        }

        /// <summary>
        /// Serialize a <see cref="Guid"/> to <see cref="ByteString"/> as a variant 1 UUID.
        /// </summary>
        /// <param name="value">
        /// The <see cref="Guid"/> to serialize to variant 1 UUID.
        /// </param>
        /// <returns>
        /// <see cref="ByteString"/> that contains the raw data of variant 1 UUID.
        /// </returns>
        /// <remarks>
        /// This method produce a different result from <c>ByteString.CopyFrom(guid.ToByteArray())</c>, which is not
        /// correct due to incompatible endianness.
        /// </remarks>
        public static ByteString Serialize(Guid value)
        {
            // Guid in .NET is variant 1 UUID with time_low, time_mid and time_hi_and_version in little endian, which is
            // incompatible with UUID on the other platforms. So we need to convert those fields to big endian.
            using var buffer = MemoryPool<byte>.Shared.Rent(16);
            var output = buffer.Memory.Span;

            if (!value.TryWriteBytes(output))
            {
                // This should never happen.
                throw new NotSupportedException($"Cannot serialize {typeof(Guid)}.");
            }

            if (!IsVariant1UUID(output))
            {
                // This should never happen.
                throw new ArgumentException("The value must be varian 1 UUID.", nameof(value));
            }

            // Swap endianess for the first 3 fields.
            var a = BinaryPrimitives.ReadInt32LittleEndian(output.Slice(0));
            var b = BinaryPrimitives.ReadInt16LittleEndian(output.Slice(4));
            var c = BinaryPrimitives.ReadInt16LittleEndian(output.Slice(6));

            BinaryPrimitives.WriteInt32BigEndian(output.Slice(0), a);
            BinaryPrimitives.WriteInt16BigEndian(output.Slice(4), b);
            BinaryPrimitives.WriteInt16BigEndian(output.Slice(6), c);

            return ByteString.CopyFrom(output.Slice(0, 16));
        }

        private static bool IsVariant1UUID(ReadOnlySpan<byte> raw)
        {
            return (raw[8] & 0xC0) == 0x80;
        }
    }
}
