namespace Polybus.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using Polybus.Example;
    using Xunit;

    public sealed class ConsumerIndexTests
    {
        public static readonly IEnumerable<object[]> SupportedEvents = FakeEventConsumer.SupportedEvents
            .Select(e => new object[] { e })
            .ToList();

        private readonly FakeEventConsumer consumer;
        private readonly ConsumerIndex subject;

        public ConsumerIndexTests()
        {
            this.consumer = new FakeEventConsumer();
            this.subject = new ConsumerIndex(new[] { this.consumer });
        }

        [Fact]
        public void Constructor_WithNonEmptyConsumers_ShouldInitializeCorrectly()
        {
            // Arrange.
            var person = new Person();
            var addressBook = new AddressBook();
            var personConsuming = new TaskCompletionSource<bool>();
            var addressBookConsuming = new TaskCompletionSource<bool>();

            this.consumer.StubbedConsumeAddressBookAsync
                .Setup(f => f(It.IsAny<AddressBook>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<bool>(addressBookConsuming.Task));

            this.consumer.StubbedConsumePersonAsync
                .Setup(f => f(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<bool>(personConsuming.Task));

            // Assert.
            Assert.Equal(2, this.subject.Count);

            var personConsumer = Assert.Contains(Person.Descriptor.FullName, this.subject);
            var addressBookConsumer = Assert.Contains(AddressBook.Descriptor.FullName, this.subject);

            Assert.Same(Person.Descriptor, personConsumer.EventDescriptor);
            Assert.Same(Person.Parser, personConsumer.EventParser);
            Assert.Same(AddressBook.Descriptor, addressBookConsumer.EventDescriptor);
            Assert.Same(AddressBook.Parser, addressBookConsumer.EventParser);

            this.consumer.ClearStubbedInvocations();

            using (var cancellation = new CancellationTokenSource())
            {
                var result = personConsumer.ConsumeExecutor(person, cancellation.Token);

                this.consumer.StubbedConsumePersonAsync.Verify(
                    f => f(person, cancellation.Token),
                    Times.Once());
                this.consumer.StubbedConsumeAddressBookAsync.Verify(
                    f => f(It.IsAny<AddressBook>(), It.IsAny<CancellationToken>()),
                    Times.Never());

                Assert.Same(result.AsTask(), personConsuming.Task);
            }

            this.consumer.ClearStubbedInvocations();

            using (var cancellation = new CancellationTokenSource())
            {
                var result = addressBookConsumer.ConsumeExecutor(addressBook, cancellation.Token);

                this.consumer.StubbedConsumePersonAsync.Verify(
                    f => f(It.IsAny<Person>(), It.IsAny<CancellationToken>()),
                    Times.Never());
                this.consumer.StubbedConsumeAddressBookAsync.Verify(
                    f => f(addressBook, cancellation.Token),
                    Times.Once());

                Assert.Same(result.AsTask(), addressBookConsuming.Task);
            }
        }

        [Theory]
        [MemberData(nameof(SupportedEvents))]
        public void Item_GetWithExistenceKey_ShouldReturnCorrespondingValue(string key)
        {
            var value = this.subject[key];

            Assert.Equal(key, value.EventDescriptor.FullName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        public void Item_GetWithNonExistenceKey_ShouldThrow(string key)
        {
            Assert.Throws<KeyNotFoundException>(() => this.subject[key]);
        }
    }
}
