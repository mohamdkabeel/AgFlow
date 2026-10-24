using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Clients.Entities;

public class RetainerAgreement : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ClientId { get; private set; }
    public int MonthlyHours { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public bool RolloverEnabled { get; private set; }
    public bool IsActive { get; private set; }

    // بيحدد أول شهر يتفوتر. من غيره Retainer اتعمل النهاردة كان هيتفوتر على شهر فات قبل ما يبدأ أصلاً
    public DateOnly StartDate { get; private set; }

    // لما الـ Retainer بيتوقف، آخر شهر اشتغلنا فيه لسه لازم يتفوتر. من غير EndDate،
    // IsActive=false كان هيخلي الـ Job يتجاهله وآخر شهر يضيع من غير فاتورة
    public DateOnly? EndDate { get; private set; }

    private RetainerAgreement() { }

    // FR-CM-3: Owner/Admin فقط (بيتفرض عن طريق [Authorize] في الـ Controller)
    public static RetainerAgreement Create(
        Guid workspaceId, Guid clientId, int monthlyHours, decimal monthlyPrice,
        bool rolloverEnabled, DateOnly startDate)
    {
        if (monthlyHours <= 0)
            throw new ArgumentException("Monthly hours must be positive.", nameof(monthlyHours));
        if (monthlyPrice <= 0)
            throw new ArgumentException("Monthly price must be positive.", nameof(monthlyPrice));

        return new RetainerAgreement
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClientId = clientId,
            MonthlyHours = monthlyHours,
            MonthlyPrice = monthlyPrice,
            RolloverEnabled = rolloverEnabled,
            IsActive = true,
            StartDate = startDate
        };
    }

    public void Deactivate(DateOnly endDate)
    {
        if (!IsActive)
            throw new DomainException("This retainer is already deactivated.");
        if (endDate < StartDate)
            throw new DomainException("End date cannot be before the retainer's start date.");

        IsActive = false;
        EndDate = endDate;
    }

    // هل الـ Retainer ده لسه مستحق عليه فاتورة لفترة تبدأ في periodStart؟
    public bool IsBillableForPeriod(DateOnly periodStart) =>
        StartDate <= periodStart.AddMonths(1).AddDays(-1) && (IsActive || (EndDate is { } end && end >= periodStart));
}
