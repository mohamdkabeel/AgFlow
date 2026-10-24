using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Projects.Entities;

public enum TaskItemStatus
{
    ToDo,
    InProgress,
    InReview,
    Done
}

public class TaskItem : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TaskListId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // FR-PM-4/5: Task ممكن يكون ليها أكتر من شخص متكلف (Many-to-Many عبر TaskAssignment)
    private readonly List<TaskAssignment> _assignments = new();
    public IReadOnlyCollection<TaskAssignment> Assignments => _assignments.AsReadOnly();

    private TaskItem() { }

    // FR-PM-4
    public static TaskItem Create(
        Guid workspaceId, Guid taskListId, string title, string? description, DateTime? dueDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title cannot be empty.", nameof(title));

        return new TaskItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            TaskListId = taskListId,
            Title = title,
            Description = description,
            DueDate = dueDate,
            Status = TaskItemStatus.ToDo,
            CreatedAt = DateTime.UtcNow
        };
    }

    // بيمنع تكليف نفس الشخص مرتين بالغلط على نفس الـ Task
    public void AssignTo(Guid userId)
    {
        if (_assignments.Any(a => a.UserId == userId))
            return;

        _assignments.Add(TaskAssignment.Create(Id, userId));
    }

    public void UpdateStatus(TaskItemStatus newStatus)
    {
        Status = newStatus;
    }
}
