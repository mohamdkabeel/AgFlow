using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Clients.Commands.UpdateClient;

public record UpdateClientCommand(Guid ClientId, string CompanyName) : IRequest;

public class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
    }
}

public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand>
{
    private readonly IApplicationDbContext _dbContext;
    public UpdateClientCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(UpdateClientCommand request, CancellationToken ct)
    {
        var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found.");

        client.Rename(request.CompanyName);
        await _dbContext.SaveChangesAsync(ct);
    }
}
