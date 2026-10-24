using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Clients.Commands.DeactivateRetainer;

// تعديل سعر الـ Retainer مفيش له أمر Update عمداً: بنوقف القديم وننشئ جديد.
// كده تاريخ الأسعار محفوظ تلقائياً (US-9.1) والفواتير القديمة تفضل مرتبطة بالعقد اللي اتحسبت عليه
public record DeactivateRetainerCommand(Guid RetainerId, DateOnly? EndDate = null) : IRequest;

public class DeactivateRetainerCommandValidator : AbstractValidator<DeactivateRetainerCommand>
{
    public DeactivateRetainerCommandValidator()
    {
        RuleFor(x => x.RetainerId).NotEmpty();
        RuleFor(x => x.EndDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1))
            .When(x => x.EndDate.HasValue);
    }
}

public class DeactivateRetainerCommandHandler : IRequestHandler<DeactivateRetainerCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeactivateRetainerCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(DeactivateRetainerCommand request, CancellationToken ct)
    {
        var retainer = await _dbContext.RetainerAgreements
            .FirstOrDefaultAsync(r => r.Id == request.RetainerId, ct)
            ?? throw new KeyNotFoundException("Retainer not found.");

        retainer.Deactivate(request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
        await _dbContext.SaveChangesAsync(ct);
    }
}
