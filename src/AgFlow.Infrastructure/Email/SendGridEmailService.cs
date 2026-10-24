using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Infrastructure.Email;

public class SendGridEmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly string _fromEmail;

    public SendGridEmailService(IConfiguration configuration)
    {
        _client = new SendGridClient(configuration["SendGrid:ApiKey"]);
        _fromEmail = configuration["SendGrid:FromEmail"]!;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(_fromEmail, "AgFlow"),
            new EmailAddress(toEmail),
            subject,
            plainTextContent: body,
            htmlContent: body);

        await _client.SendEmailAsync(message);
    }
}
