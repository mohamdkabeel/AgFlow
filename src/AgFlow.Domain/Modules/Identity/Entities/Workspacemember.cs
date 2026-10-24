using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Identity.Entities;

public enum WorkspaceRole
{
    Owner,
    Admin,
    ProjectManager,
    TeamMember,
    Finance
}

public enum MembershipStatus
{
    Active,
    Deactivated
}

public class WorkspaceMember : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public decimal HourlyCost { get; private set; } // بيتحدد وقت الانضمام، وقابل للتعديل بعدين
    public DateTime JoinedAt { get; private set; }

    private WorkspaceMember() { }

    // أول عضو في أي Workspace لازم يكون Owner دايماً — عبّرنا عن القاعدة دي
    // كـ Factory Method منفصلة بدل ما نسيب أي كود بره يقدر ينشئ Owner عشوائي
    public static WorkspaceMember CreateOwner(Guid workspaceId, Guid userId)
    {
        return new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = WorkspaceRole.Owner,
            Status = MembershipStatus.Active,
            HourlyCost = 0, // الـ Owner عادة مش بيتحسب على نفسه تكلفة ساعة
            JoinedAt = DateTime.UtcNow
        };
    }

    // للأعضاء اللي بينضموا بعد كده عن طريق دعوة (هنستخدمها لاحقاً)
    public static WorkspaceMember CreateFromInvitation(
        Guid workspaceId, Guid userId, WorkspaceRole role, decimal hourlyCost)
    {
        if (role == WorkspaceRole.Owner)
            throw new DomainException("Cannot invite a member directly as Owner.");

        return new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = role,
            Status = MembershipStatus.Active,
            HourlyCost = hourlyCost,
            JoinedAt = DateTime.UtcNow
        };
    }

    // مهم: ده بيغيّر التكلفة "من دلوقتي فصاعداً" بس — مش بيلمس أي TimeEntry
    // قديمة خالص، لأنها خزّنت Snapshot بالفعل (Phase 3, Step 1)
    public void UpdateHourlyCost(decimal newHourlyCost)
    {
        if (newHourlyCost < 0)
            throw new ArgumentException("Hourly cost cannot be negative.");

        HourlyCost = newHourlyCost;
    }

    // زي ما اتفقنا في السؤال اللي فات — تعطيل مش حذف
    public void Deactivate()
    {
        // الـ Workspace من غير Owner نشط يبقى من غير حد يقدر يدير الاشتراك أو الصلاحيات
        if (Role == WorkspaceRole.Owner)
            throw new DomainException("The workspace Owner cannot be deactivated.");
        Status = MembershipStatus.Deactivated;
    }

    public void ChangeRole(WorkspaceRole newRole)
    {
        if (Role == WorkspaceRole.Owner)
            throw new DomainException("The Owner's role cannot be changed.");
        if (newRole == WorkspaceRole.Owner)
            throw new DomainException("A member cannot be promoted to Owner.");
        if (Status != MembershipStatus.Active)
            throw new DomainException("Cannot change the role of a deactivated member.");
        Role = newRole;
    }

    // عضو اتعطّل ورجع بدعوة جديدة: بنرجّعه بنفس الصف (مفيش عضوية تانية) بالدور والتكلفة الجديدين
    public void Reactivate(WorkspaceRole role, decimal hourlyCost)
    {
        if (role == WorkspaceRole.Owner)
            throw new DomainException("Cannot reactivate a member as Owner.");
        Role = role;
        HourlyCost = hourlyCost;
        Status = MembershipStatus.Active;
    }
}
