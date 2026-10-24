using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.TimeTracking.Entities;

public class TimeEntry : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TaskItemId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal DurationHours { get; private set; }

    // يوم الشغل الفعلي (مش وقت التسجيل). الفوترة والربحية بيتحسبوا بيه: ساعات اشتغلتها 28 سبتمبر
    // وسجّلتها 2 أكتوبر لازم تتحسب على سبتمبر، وده كمان اللي بيسمح بتسجيل وقت نسيته (FR-TT-2)
    public DateOnly WorkDate { get; private set; }

    // مش FK لجدول User — رقم مخزّن ثابت وقت الإنشاء، مش هيتغير أبداً بعد كده (FR-TT-3)
    public decimal HourlyCostAtTimeOfEntry { get; private set; }

    public bool IsBillable { get; private set; }
    public bool IsApproved { get; private set; }

    // مين وافق ومتى. null مع IsApproved=true معناها "اتعتمد تلقائياً" (الـ Workspace مش مفعّل الموافقة).
    // مهم لأن الموافقة قرار بيحوّل ساعات لفلوس على فاتورة، فلازم يبقى فيه مسؤول عنه (US-9.1)
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TimeEntry() { }

    public static TimeEntry Log(
        Guid workspaceId,
        Guid taskItemId,
        Guid userId,
        DateOnly workDate,
        decimal durationHours,
        decimal currentUserHourlyCost,
        bool isBillable,
        string? note,
        bool requiresApproval)
    {
        if (durationHours <= 0)
            throw new ArgumentException("Duration must be positive.", nameof(durationHours));
        // +1 يوم تسامح: المستخدم في Cairo (UTC+3) الساعة 1 بعد نص الليل، لسه "امبارح" بتوقيت UTC
        if (workDate > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            throw new ArgumentException("Work date cannot be in the future.", nameof(workDate));

        return new TimeEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            TaskItemId = taskItemId,
            UserId = userId,
            WorkDate = workDate,
            DurationHours = durationHours,
            HourlyCostAtTimeOfEntry = currentUserHourlyCost,
            IsBillable = isBillable,
            // FR-TT-4: الموافقة اختيارية. لو الـ Workspace مش مفعّلها، الإدخال بيتعتمد تلقائياً.
            // (الباج القديم: كان دايماً false، فالفواتير كانت بتحسب صفر ساعات للأبد)
            IsApproved = !requiresApproval,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateDuration(decimal newDurationHours)
    {
        if (IsApproved)
            throw new DomainException("Cannot edit an approved time entry.");

        if (newDurationHours <= 0)
            throw new ArgumentException("Duration must be positive.", nameof(newDurationHours));

        DurationHours = newDurationHours;
    }

    public void Approve(Guid approvedByUserId)
    {
        if (IsApproved)
            return; // Idempotent: الموافقة مرتين مش بتغيّر اسم أول مين وافق ولا تاريخه

        IsApproved = true;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;
    }
}
