using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Projects.Entities;

public class TaskList : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private TaskList() { }

    // FR-PM-3
    public static TaskList Create(Guid workspaceId, Guid projectId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Task list name cannot be empty.", nameof(name));

        return new TaskList
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Name = name
        };
    }
}
