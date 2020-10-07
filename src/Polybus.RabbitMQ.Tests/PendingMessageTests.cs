namespace Polybus.RabbitMQ.Tests
{
    using System.Collections.Generic;
    using Xunit;

    public sealed class PendingMessageTests
    {
        private readonly PendingMessage subject;

        public PendingMessageTests()
        {
            this.subject = new PendingMessage(5, 10);
        }

        [Fact]
        public void CreateLookupKey_WhenInvoke_ResultShouldBeUsedInSortedSetCorrectly()
        {
            // Act.
            var lookup1 = PendingMessage.CreateLookupKey(0, 11);
            var lookup2 = PendingMessage.CreateLookupKey(5, 11);
            var lookup3 = PendingMessage.CreateLookupKey(6, 0);

            // Assert.
            var set = new SortedSet<PendingMessage>()
            {
                this.subject,
                lookup1,
                lookup2,
                lookup3,
            };

            Assert.Equal(new[] { lookup1, this.subject, lookup2, lookup3 }, set);
        }
    }
}
