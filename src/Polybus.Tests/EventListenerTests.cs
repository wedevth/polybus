namespace Polybus.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using Polybus.Example;
    using Xunit;

    public sealed class EventListenerTests
    {
        private readonly FakeEventConsumer consumer;
        private readonly FakeEventListener subject;

        public EventListenerTests()
        {
            this.consumer = new FakeEventConsumer();
            this.subject = new FakeEventListener();
        }

        [Fact]
        public void InitializeConsumerTable_WithNonEmptyConsumers_ShouldInvokeCallbackWithCorrectArgument()
        {
            // Arrange.
            var callback = new Mock<Action<ConsumerDescriptor>>();
            var person = new Person();
            var addressBook = new AddressBook();
            var personConsuming = new TaskCompletionSource<bool>();
            var addressBookConsuming = new TaskCompletionSource<bool>();

            this.consumer.StubbedConsumeAddressBookAsync
                .Setup(f => f(It.IsAny<AddressBook>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask(addressBookConsuming.Task));

            this.consumer.StubbedConsumePersonAsync
                .Setup(f => f(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask(personConsuming.Task));

            // Act.
            this.subject.InitializeConsumerTable(new[] { this.consumer }, callback.Object);

            // Assert.
            callback.Verify(
                f => f(It.Is<ConsumerDescriptor>(a => ReferenceEquals(a.Instance, this.consumer))),
                Times.Exactly(2));

            var personConsumer = callback.Invocations
                .Select(i => (ConsumerDescriptor)i.Arguments[0])
                .Single(d => d.EventDescriptor.ClrType == typeof(Person));

            var addressBookConsumer = callback.Invocations
                .Select(i => (ConsumerDescriptor)i.Arguments[0])
                .Single(d => d.EventDescriptor.ClrType == typeof(AddressBook));

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
    }
}
