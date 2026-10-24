using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Billing.Entities;

namespace AgFlow.Application.Modules.Billing.Commands.ApproveOverage;

public record ApproveOverageCommand(Guid OverageApprovalId, Guid InvoiceId) : IRequest;

public class ApproveOverageCommandHandler : IRequestHandler<ApproveOverageCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ApproveOverageCommandHandler(
        IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ApproveOverageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No user context found.");

        var overage = await _dbContext.OverageApprovals
            .FirstOrDefaultAsync(o => o.Id == request.OverageApprovalId, cancellationToken)
            ?? throw new KeyNotFoundException("Overage approval not found.");

        var invoice = await _dbContext.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new KeyNotFoundException("Invoice not found.");

        // فحوصات تماسك (مش Tenant، لأن الاتنين اتقروا من خلال الفلتر): الـ Overage ده لازم يخص
        // نفس عميل الفاتورة، والفاتورة لسه Draft. من غير كده Owner يقدر يحط Overage عميل A
        // على فاتورة عميل B، أو يعدّل فاتورة اتبعتت فعلاً للعميل.
        if (overage.ClientId != invoice.ClientId)
            throw new ArgumentException("This overage does not belong to the invoice's client.");
        // الـ Overage بيتحسب لفترة معينة، فمينفعش يتحط على فاتورة شهر تاني
        if (overage.InvoiceId != invoice.Id)
            throw new ArgumentException("This overage was calculated for a different invoice.");
        if (invoice.Status != InvoiceStatus.Draft)
            throw new ArgumentException("Overage can only be added to a draft invoice.");

        // القرار البشري الفعلي بيحصل هنا — لحظة استدعاء Approve()
        overage.Approve(userId);

        // بس دلوقتي، بعد الموافقة الصريحة، الـ Overage بيتضاف كـ Line Item فعلي للفاتورة
        invoice.AddLineItem($"Overage ({overage.OverageHours}h)", overage.OverageAmount);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
