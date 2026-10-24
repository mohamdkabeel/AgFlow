using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Billing.Entities;

namespace AgFlow.Application.Modules.Billing.Commands.GenerateDraftInvoice;

// Year/Month اختياريين: من غيرهم = الشهر اللي فات (الاستخدام العادي من الـ Job).
// معاهم = فترة محددة (Finance عايز يولّد فاتورة شهر قديم يدوياً).
public record GenerateDraftInvoiceCommand(Guid RetainerAgreementId, int? Year = null, int? Month = null)
    : IRequest<Guid>;

public class GenerateDraftInvoiceCommandValidator : AbstractValidator<GenerateDraftInvoiceCommand>
{
    public GenerateDraftInvoiceCommandValidator()
    {
        RuleFor(x => x.Month).InclusiveBetween(1, 12).When(x => x.Month.HasValue);
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100).When(x => x.Year.HasValue);
        RuleFor(x => x)
            .Must(x => x.Year.HasValue == x.Month.HasValue)
            .WithMessage("Provide both Year and Month, or neither.");
    }
}

public class GenerateDraftInvoiceCommandHandler : IRequestHandler<GenerateDraftInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GenerateDraftInvoiceCommandHandler(
        IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(GenerateDraftInvoiceCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        // === 1) فترة الفوترة: شهر ميلادي كامل، والافتراضي الشهر اللي فات ===
        // (النسخة القديمة كانت بتحسب من أول الشهر الحالي لحد دلوقتي، يعني فاتورة بنص شهر)
        var today = DateTime.UtcNow.Date;
        var periodStart = request.Year is int y && request.Month is int m
            ? new DateTime(y, m, 1)
            : new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var periodEnd = periodStart.AddMonths(1);

        if (periodEnd > today)
            throw new ArgumentException("The billing period has not ended yet.");

        var retainer = await _dbContext.RetainerAgreements
            .FirstOrDefaultAsync(r => r.Id == request.RetainerAgreementId, ct)
            ?? throw new KeyNotFoundException("Retainer agreement not found.");

        // Retainer ماكانش بدأ في الفترة دي، أو اتوقف قبل ما تبدأ → مفيش حاجة تتفوتر
        if (!retainer.IsBillableForPeriod(DateOnly.FromDateTime(periodStart)))
            throw new ArgumentException("This retainer is not billable for the requested period.");

        // === 2) Idempotency: لو الفاتورة دي اتولدت قبل كده، نرجّعها ومانولّدش تانية ===
        // الـ Job ممكن يتنفذ مرتين (Retry، إعادة تشغيل)، والأمر ده لازم يبقى آمن مهما اتكرر
        var existingId = await FindExistingAsync(retainer.Id, periodStart, ct);
        if (existingId is Guid already)
            return already;

        var client = await _dbContext.Clients
            .FirstOrDefaultAsync(c => c.Id == retainer.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found.");

        // === 3) الساعات الفعلية: استعلام واحد بدل 3 قوايم في الذاكرة ===
        // Project → TaskList → TaskItem → TimeEntry (نفس السلسلة في الـ ERD)
        var periodStartDay = DateOnly.FromDateTime(periodStart);
        var periodEndDay = DateOnly.FromDateTime(periodEnd);
        var actualHours = await (
            from te in _dbContext.TimeEntries
            join ti in _dbContext.TaskItems on te.TaskItemId equals ti.Id
            join tl in _dbContext.TaskLists on ti.TaskListId equals tl.Id
            join p in _dbContext.Projects on tl.ProjectId equals p.Id
            where p.RetainerAgreementId == retainer.Id
                && te.IsBillable
                && te.IsApproved // FR-TT-6
                && te.WorkDate >= periodStartDay && te.WorkDate < periodEndDay
            select te.DurationHours).SumAsync(ct);

        // === 4) الفاتورة (Draft دايماً) ===
        var invoice = Invoice.CreateDraft(
            workspaceId, client.Id, client.CompanyName, retainer.Id, periodStart, periodEnd);
        invoice.AddLineItem(
            $"Monthly retainer {periodStart:yyyy-MM} ({retainer.MonthlyHours}h agreed)", retainer.MonthlyPrice);

        // === 5) Overage: طلب موافقة منفصل، مش بيدخل الفاتورة تلقائي (FR-BI-3) ===
        if (actualHours > retainer.MonthlyHours)
        {
            var overageHours = actualHours - retainer.MonthlyHours;
            // تنبيه: ده سعر تقريبي (سعر الـ Retainer ÷ ساعاته). لما Finance يوافق، المبلغ ده
            // بيتحول لفلوس فعلية على الفاتورة، فسعر ساعة الزيادة لازم يبقى بند في العقد (قرار بزنس مفتوح)
            var overageAmount = Math.Round(overageHours * (retainer.MonthlyPrice / retainer.MonthlyHours), 2);

            _dbContext.OverageApprovals.Add(
                OverageApproval.CreatePending(workspaceId, client.Id, invoice.Id, overageHours, overageAmount));
        }

        _dbContext.Invoices.Add(invoice);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // سباق: طلب تاني ولّد نفس الفاتورة بين فحصنا وحفظنا، والـ Unique Index منع التكرار.
            // لو لقينا الفائزة نرجّعها، لو لأ يبقى الخطأ حاجة تانية ومانخبيهوش.
            var winner = await FindExistingAsync(retainer.Id, periodStart, ct);
            if (winner is Guid winnerId)
                return winnerId;
            throw;
        }

        return invoice.Id;
    }

    private Task<Guid?> FindExistingAsync(Guid retainerId, DateTime periodStart, CancellationToken ct) =>
        _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.RetainerAgreementId == retainerId
                && i.PeriodStart == periodStart
                && i.Status != InvoiceStatus.Void)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);
}
