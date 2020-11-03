namespace Polybus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using Polybus.Example;

    sealed class FakeEventConsumer : IEventConsumer<AddressBook>, IEventConsumer<Person>
    {
        public static readonly IEnumerable<string> SupportedEvents = new[]
        {
            AddressBook.Descriptor.FullName,
            Person.Descriptor.FullName,
        };

        public FakeEventConsumer()
        {
            this.StubbedConsumeAddressBookAsync = new Mock<Func<AddressBook, CancellationToken, ValueTask<bool>>>();
            this.StubbedConsumePersonAsync = new Mock<Func<Person, CancellationToken, ValueTask<bool>>>();
        }

        public Mock<Func<AddressBook, CancellationToken, ValueTask<bool>>> StubbedConsumeAddressBookAsync { get; }

        public Mock<Func<Person, CancellationToken, ValueTask<bool>>> StubbedConsumePersonAsync { get; }

        public void ClearStubbedInvocations()
        {
            this.StubbedConsumeAddressBookAsync.Invocations.Clear();
            this.StubbedConsumePersonAsync.Invocations.Clear();
        }

        public ValueTask<bool> ConsumeEventAsync(AddressBook @event, CancellationToken cancellationToken = default)
        {
            return this.StubbedConsumeAddressBookAsync.Object(@event, cancellationToken);
        }

        public ValueTask<bool> ConsumeEventAsync(Person @event, CancellationToken cancellationToken = default)
        {
            return this.StubbedConsumePersonAsync.Object(@event, cancellationToken);
        }
    }
}
