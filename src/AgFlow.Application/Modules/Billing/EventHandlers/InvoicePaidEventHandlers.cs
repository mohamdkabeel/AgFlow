using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Billing.Events;
using AgFlow.Domain.Modules.Notifications.Entities;

namespace AgFlow.Application.Modules.Billing.EventHandlers;

// Handler الأول: بيسجل In-App Notification
public class CreateNotificationOnInvoicePaid : INotificationHandler<InvoicePaidEvent>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateNotificationOnInvoicePaid(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(InvoicePaidEvent notification, CancellationToken cancellationToken)
    {
        // بنوجه الإشعار للـ Owners/Admins في الـ Workspace ده (مثال مبسط: كل الأعضاء هنا)
        var recipientIds = await _dbContext.WorkspaceMembers
            .IgnoreQueryFilters() // نفس الاستثناء الموثّق من Step قبل كده — بنتصرف هنا "نيابة عن النظام"
            .Where(m => m.WorkspaceId == notification.WorkspaceId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in recipientIds)
        {
            var inAppNotification = Notification.Create(
                notification.WorkspaceId, userId,
                $"Invoice of {notification.Amount:C} was marked as paid.");
            _dbContext.Notifications.Add(inAppNotification);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

// Handler التاني: بيبعت Email — منفصل تماماً عن الأول (Single Responsibility)
public class SendEmailOnInvoicePaid : INotificationHandler<InvoicePaidEvent>
{
    private readonly IEmailService _emailService;

    public SendEmailOnInvoicePaid(IEmailService emailService) => _emailService = emailService;

    public Task Handle(InvoicePaidEvent notification, CancellationToken cancellationToken)
    {
        // في نسخة حقيقية، هنجيب إيميل الـ Owner من الـ Database هنا
        return _emailService.SendAsync(
            "owner@example.com",
            "Payment Received",
            $"Your invoice of {notification.Amount:C} has been marked as paid. Thank you!");
    }
}
