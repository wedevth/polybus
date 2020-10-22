namespace Polybus.Serialization.Tests
{
    using System;
    using Google.Protobuf;
    using Xunit;

    public sealed class EventSerializerTests
    {
        // afc2b8be-250d-4d56-9641-a58198a938b6
        private static readonly byte[] SampleUuid = new byte[]
        {
            0xAF, 0xC2, 0xB8, 0xBE,             // time_low
            0x25, 0x0D,                         // time_mid
            0x4D, 0x56,                         // time_hi_and_version
            0x96,                               // clock_seq_hi_and_reserved (100xxxxx)
            0x41,                               // clock_seq_low
            0xA5, 0x81, 0x98, 0xA9, 0x38, 0xB6, // node
        };

        private static readonly Guid SampleGuid = Guid.Parse("afc2b8be-250d-4d56-9641-a58198a938b6");

        [Fact]
        public void DeserializeGuid_WithVariant1UUID_ShouldReturnCorrectValue()
        {
            var result = EventSerializer.DeserializeGuid(ByteString.CopyFrom(SampleUuid));

            Assert.Equal("afc2b8be-250d-4d56-9641-a58198a938b6", result.ToString());
        }

        [Fact]
        public void Serialize_WithRandomGuid_ShouldSerializeSuccess()
        {
            for (var i = 0; i < 10; i++)
            {
                EventSerializer.Serialize(Guid.NewGuid());
            }
        }

        [Fact]
        public void Serialize_WithGuid_ShouldReturnVariant1UUID()
        {
            var result = EventSerializer.Serialize(SampleGuid);

            Assert.Equal(SampleUuid, result);
        }
    }
}
