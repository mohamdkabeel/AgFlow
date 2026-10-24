namespace AgFlow.Application.Common.Interfaces;

// الـ Application مش عارف إن ده جاي من JWT أو HTTP أصلاً — بس عارف إنه "فيه حد بيسأل عن نفسه"
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? WorkspaceId { get; }
    string? Role { get; }

    // بتتيح لـ Background Jobs (من غير HTTP Request) تحدد WorkspaceId
    // لفترة تنفيذ معينة — الـ Interface بس بيعرّف الإمكانية، التنفيذ جوه Infrastructure
    IDisposable RunAsWorkspace(Guid workspaceId);
}
