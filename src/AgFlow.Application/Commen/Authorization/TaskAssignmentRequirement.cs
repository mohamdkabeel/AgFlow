using Microsoft.AspNetCore.Authorization;

namespace AgFlow.Application.Common.Authorization;

// ده مجرد "علامة" بتقول "الـ Endpoint ده محتاج تحقق من نوع TaskAssignment"
// مفيهاش منطق فعلي — المنطق هيبقى في الـ Handler اللي جاي تحت
public class TaskAssignmentRequirement : IAuthorizationRequirement
{
}
