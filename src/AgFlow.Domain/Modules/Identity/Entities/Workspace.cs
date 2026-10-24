namespace AgFlow.Domain.Modules.Identity.Entities;

public class Workspace
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Subdomain { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // FR-TT-4: لو true، كل TimeEntry لازم يتوافق عليه قبل ما يتحسب في الفواتير.
    // Property واحدة بس: اللي LogTimeEntry بيقراها وSetTimeApprovalCommand بتكتبها
    public bool RequireTimeApproval { get; private set; }

    private Workspace() { }

    public static Workspace Create(string name, string subdomain)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(subdomain))
            throw new ArgumentException("Workspace subdomain cannot be empty.", nameof(subdomain));

        return new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Subdomain = subdomain.Trim(),
            CreatedAt = DateTime.UtcNow,
            RequireTimeApproval = false
        };
    }

    public void SetTimeApprovalRequired(bool required) => RequireTimeApproval = required;
}