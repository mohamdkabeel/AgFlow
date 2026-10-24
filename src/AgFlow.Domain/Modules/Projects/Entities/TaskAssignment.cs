namespace AgFlow.Domain.Modules.Projects.Entities;

// جدول Many-to-Many حقيقي (Phase 5, Step 1) — مش مجرد ربط، فيه بيانات خاصة بيه
public class TaskAssignment
{
    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    private TaskAssignment() { }

    internal static TaskAssignment Create(Guid taskItemId, Guid userId)
    {
        return new TaskAssignment
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskItemId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow
        };
    }
}
