using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Application.Modules.Clients.Commands.CreateRetainer;

// ClientId بييجي من الـ Route (مش الـ Body) في الـ Controller
public record CreateRetainerCommand(
    Guid ClientId, int MonthlyHours, decimal MonthlyPrice, bool RolloverEnabled, DateOnly StartDate)
    : IRequest<Guid>;

public class CreateRetainerCommandValidator : AbstractValidator<CreateRetainerCommand>
{
    public CreateRetainerCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.MonthlyHours).InclusiveBetween(1, 744); // 744 = ساعات أطول شهر
        RuleFor(x => x.MonthlyPrice).GreaterThan(0).LessThanOrEqualTo(10_000_000m);
        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1))
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1));
    }
}

public class CreateRetainerCommandHandler : IRequestHandler<CreateRetainerCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public CreateRetainerCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateRetainerCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        // IDOR: العميل لازم يكون من نفس الـ Workspace (الـ Query Filter بيضمن ده)
        var client = await _dbContext.Clients
            .FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found.");

        if (client.Status != ClientStatus.Active)
            throw new DomainException("Cannot add a retainer to an archived client.");

        if (await HasActiveRetainerAsync(client.Id, ct))
            throw new DomainException(
                "This client already has an active retainer. Deactivate it first, then create the new one.");

        var retainer = RetainerAgreement.Create(
            workspaceId, client.Id, request.MonthlyHours, request.MonthlyPrice,
            request.RolloverEnabled, request.StartDate);

        _dbContext.RetainerAgreements.Add(retainer);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // سباق: طلبين اتبعتوا مع بعض والـ Unique Index منع التاني. لو ده السبب نقول رسالة واضحة،
            // وغير كده الخطأ حاجة تانية ومانخبيهوش
            if (await HasActiveRetainerAsync(client.Id, ct))
                throw new DomainException("This client already has an active retainer.");
            throw;
        }

        return retainer.Id;
    }

    private Task<bool> HasActiveRetainerAsync(Guid clientId, CancellationToken ct) =>
        _dbContext.RetainerAgreements.AsNoTracking().AnyAsync(r => r.ClientId == clientId && r.IsActive, ct);
}
