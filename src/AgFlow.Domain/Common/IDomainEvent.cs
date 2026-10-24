using MediatR;

namespace AgFlow.Domain.Common;

// بتورث من INotification بتاعت MediatR مباشرة — كده أي Event ممكن
// يتبعت عبر نفس الـ Mediator اللي بنستخدمه للـ Commands/Queries، من غير آلية إضافية منفصلة
public interface IDomainEvent : INotification
{
}
