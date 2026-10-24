using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Projects.Entities;

public enum ProjectType
{
    Retainer,
    FixedScope
}

public enum ProjectStatus
{
    Active,
    Completed,
    Archived
}

public class Project : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? RetainerAgreementId { get; private set; } // Nullable — Fixed-Scope مالهاش Retainer (Phase 5, Step 2)
    public string Name { get; private set; } = string.Empty;
    public ProjectType Type { get; private set; }
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Project() { }

    // FR-PM-1 + FR-PM-2
    public static Project CreateRetainerProject(
        Guid workspaceId, Guid clientId, Guid retainerAgreementId, string name)
    {
        ValidateName(name);

        return new Project
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClientId = clientId,
            RetainerAgreementId = retainerAgreementId,
            Name = name,
            Type = ProjectType.Retainer,
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Project CreateFixedScopeProject(Guid workspaceId, Guid clientId, string name)
    {
        ValidateName(name);

        return new Project
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClientId = clientId,
            RetainerAgreementId = null, // صراحةً من غير Retainer
            Name = name,
            Type = ProjectType.FixedScope,
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty.", nameof(name));
    }

    // FR-PM-8
    public void Complete() => Status = ProjectStatus.Completed;
    public void Archive() => Status = ProjectStatus.Archived;
}
