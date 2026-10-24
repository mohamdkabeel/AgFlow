using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.TimeTracking.Commands.ApproveTimeEntries;

// Batch: الـ PM بيراجع مئات الإدخالات في الأسبوع، وطلب لكل واحد مش عملي.
// بيرجّع عدد اللي اتعتمدت فعلاً (اللي كانت معتمدة قبل كده مش بتتحسب)
public record ApproveTimeEntriesCommand(IReadOnlyList<Guid> TimeEntryIds) : IRequest<int>;

public class ApproveTimeEntriesCommandValidator : AbstractValidator<ApproveTimeEntriesCommand>
{
    public ApproveTimeEntriesCommandValidator()
    {
        RuleFor(x => x.TimeEntryIds)
            .NotEmpty()
            .Must(ids => ids.Count <= 200).WithMessage("You can approve at most 200 entries at once.");
        RuleForEach(x => x.TimeEntryIds).NotEmpty();
    }
}

public class ApproveTimeEntriesCommandHandler : IRequestHandler<ApproveTimeEntriesCommand, int>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ApproveTimeEntriesCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(ApproveTimeEntriesCommand request, CancellationToken ct)
    {
        var approverId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("No user context found.");

        var ids = request.TimeEntryIds.Distinct().ToList();

        // الـ Query Filter بيضمن إننا نشوف إدخالات الـ Workspace ده بس
        var entries = await _dbContext.TimeEntries
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);

        // IDOR: أي Id مش بتاعنا (أو مش موجود) بيبوّظ الطلب كله، ومانقولش أنهي واحد فيهم بالظبط
        if (entries.Count != ids.Count)
            throw new KeyNotFoundException("One or more time entries were not found.");

        // فصل المهام (Segregation of Duties): مفيش حد يوافق على ساعاته بنفسه ويحوّلها لفلوس.
        // استثناء الـ Owner لأن الـ Owner ممكن يكون شغال لوحده في الأجنسي (ردّك في Q1 على الفريق الاختياري)
        var isOwner = string.Equals(_currentUser.Role, "Owner", StringComparison.OrdinalIgnoreCase);
        if (!isOwner && entries.Any(e => e.UserId == approverId))
            throw new UnauthorizedAccessException("You cannot approve your own time entries.");

        var approvedCount = 0;
        foreach (var entry in entries)
        {
            if (entry.IsApproved) continue;
            entry.Approve(approverId);
            approvedCount++;
        }

        await _dbContext.SaveChangesAsync(ct);
        return approvedCount;
    }
}
