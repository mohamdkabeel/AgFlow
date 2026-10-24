using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Billing.Entities;
using AgFlow.Domain.Modules.Identity.Entities;
using AgFlow.Domain.Modules.TimeTracking.Entities;

namespace AgFlow.Application.Modules.TimeTracking.Commands.LogTimeEntry;

public record LogTimeEntryCommand(
    Guid TaskItemId,
    decimal DurationHours,
    bool IsBillable,
    string? Note,
    DateOnly? WorkDate = null) : IRequest<Guid>; // null = النهاردة

public class LogTimeEntryCommandHandler : IRequestHandler<LogTimeEntryCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public LogTimeEntryCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(LogTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No user context found.");

        // === Fix أمني (IDOR): الـ Task لازم تكون تابعة لنفس الـ Workspace ===
        // من غير الفحص ده، مستخدم يقدر يسجل ساعات على Task تخص عميل في Workspace تاني،
        // والساعات دي كانت هتدخل في حسابات فواتير وربحية مش بتاعته
        var taskExists = await _dbContext.TaskItems
            .AnyAsync(t => t.Id == request.TaskItemId, cancellationToken);
        if (!taskExists)
            throw new KeyNotFoundException("Task not found.");

        var workDate = request.WorkDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // لو الفترة اللي الشغل ده بيقع فيها اتفوترت خلاص، تسجيل ساعات جديدة فيها يعني ساعات مش هتتفوتر أبداً
        // (الفاتورة اتحسبت)، وبتغيّر الربحية بأثر رجعي من غير ما حد يعرف. لازم تتعمل فاتورة تعديل (مش في الـ v1)
        var retainerId = await (
            from ti in _dbContext.TaskItems
            where ti.Id == request.TaskItemId
            join tl in _dbContext.TaskLists on ti.TaskListId equals tl.Id
            join p in _dbContext.Projects on tl.ProjectId equals p.Id
            select p.RetainerAgreementId).FirstOrDefaultAsync(cancellationToken);
        if (retainerId is not null)
        {
            var day = workDate.ToDateTime(TimeOnly.MinValue);
            var alreadyInvoiced = await _dbContext.Invoices.AnyAsync(i =>
                i.RetainerAgreementId == retainerId && i.Status != InvoiceStatus.Void
                && i.PeriodStart <= day && i.PeriodEnd > day, cancellationToken);
            if (alreadyInvoiced)
                throw new DomainException(
                    "This period has already been invoiced. Time can no longer be logged for this date.");
        }

        // FR-TT-4: لو الـ Workspace مفعّل الموافقة، الإدخال بيدخل Pending، غير كده بيتعتمد فوراً.
        // Workspace مش ITenantScoped (مفيش Query Filter عليه)، فبنفلتر بالـ Id بتاع التوكن صراحةً
        var requiresApproval = await _dbContext.Workspaces
            .Where(w => w.Id == workspaceId)
            .Select(w => w.RequireTimeApproval)
            .FirstOrDefaultAsync(cancellationToken);

        // === هنا بالظبط بتحصل لحظة الـ Snapshot ===
        // التكلفة بتتجاب من WorkspaceMember (تكلفة الشخص "في الـ Workspace ده")، مش من User.
        // User واحد ممكن يبقى في أكتر من Workspace بتكلفة مختلفة في كل واحد.
        // كمان الشرط Active: عضو اتعطّل لسه معاه Access Token صالح (15 دقيقة) ما يقدرش يسجّل وقت
        // (الـ Query Filter بيضمن إن العضوية من نفس الـ Workspace بتاع الـ Token)
        var hourlyCost = await _dbContext.WorkspaceMembers
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Select(m => (decimal?)m.HourlyCost)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("You are not an active member of this workspace.");

        var timeEntry = TimeEntry.Log(
            workspaceId,
            request.TaskItemId,
            userId,
            workDate,
            request.DurationHours,
            currentUserHourlyCost: hourlyCost, // القيمة بتتنسخ هنا، مش بتتحط كـ Reference
            request.IsBillable,
            request.Note,
            requiresApproval);

        _dbContext.TimeEntries.Add(timeEntry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return timeEntry.Id;
    }
}

// Validation على شكل البيانات (مختلفة عن فحص الملكية فوق): ساعات سالبة أو 500 ساعة في اليوم الواحد
// كانت بتعدي وتبوظ حسابات الـ Overage والـ Profitability
public class LogTimeEntryCommandValidator : AbstractValidator<LogTimeEntryCommand>
{
    public LogTimeEntryCommandValidator()
    {
        RuleFor(x => x.TaskItemId).NotEmpty();
        RuleFor(x => x.DurationHours)
            .GreaterThan(0).WithMessage("Duration must be greater than zero.")
            .LessThanOrEqualTo(24).WithMessage("A single entry cannot exceed 24 hours.");
        RuleFor(x => x.Note).MaximumLength(1000);
        // أقصى رجوع 31 يوم (نسيت أسجّل). أبعد من كده الفترة غالباً اتفوترت وبيانات قديمة مش المفروض تتعدّل
        RuleFor(x => x.WorkDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-31))
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .When(x => x.WorkDate.HasValue);
    }
}
