namespace Polybus.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using Polybus.Example;

    sealed class FakeEventConsumer : IEventConsumer<AddressBook>, IEventConsumer<Person>
    {
        public FakeEventConsumer()
        {
            this.StubbedConsumeAddressBookAsync = new Mock<Func<AddressBook, CancellationToken, ValueTask>>();
            this.StubbedConsumePersonAsync = new Mock<Func<Person, CancellationToken, ValueTask>>();
        }

        public Mock<Func<AddressBook, CancellationToken, ValueTask>> StubbedConsumeAddressBookAsync { get; }

        public Mock<Func<Person, CancellationToken, ValueTask>> StubbedConsumePersonAsync { get; }

        public void ClearStubbedInvocations()
        {
            this.StubbedConsumeAddressBookAsync.Invocations.Clear();
            this.StubbedConsumePersonAsync.Invocations.Clear();
        }

        public ValueTask ConsumeEventAsync(AddressBook @event, CancellationToken cancellationToken = default)
        {
            return this.StubbedConsumeAddressBookAsync.Object(@event, cancellationToken);
        }

        public ValueTask ConsumeEventAsync(Person @event, CancellationToken cancellationToken = default)
        {
            return this.StubbedConsumePersonAsync.Object(@event, cancellationToken);
        }
    }
}
