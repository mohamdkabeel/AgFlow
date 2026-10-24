namespace AgFlow.Domain.Common;

// أي Entity عايزة "تعلن" عن حدث مهم (زي Invoice لما تتدفع) بتنفذ الـ Interface ده
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
