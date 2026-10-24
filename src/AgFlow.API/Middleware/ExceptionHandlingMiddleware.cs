using System.Text.Json;
using FluentValidation;
using AgFlow.Application.Common.Exceptions;
using AgFlow.Domain.Common;

namespace AgFlow.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // كان فيه تعليق في النسخة الأولى: "هنوسّعه عشان يفرّق بين أنواع الأخطاء" — ومتوسّعش.
        // النتيجة: كل Exception (حتى باسوورد غلط!) كان بيرجع 500. هنا التصحيح.
        var (status, message, errors) = Map(ex);

        if (status >= 500)
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
        else
            _logger.LogWarning("Request rejected ({Status}): {Type}", status, ex.GetType().Name);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var body = new
        {
            status,
            message,
            errors,
            correlationId = context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }

    private static (int Status, string Message, object? Errors) Map(Exception ex)
    {
        if (ex is ValidationException ve)
        {
            var errors = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return (400, "Validation failed.", errors);
        }
        if (ex is SubscriptionReadOnlyException)
            return (402, "This workspace is in read-only mode. Upgrade your plan to make changes.",
                new { code = "subscription_read_only" });
        if (ex is InvalidCredentialsException)
            return (401, "Invalid credentials.", null);
        if (ex is UnauthorizedAccessException)
            return (403, "You do not have access to this resource.", null);
        if (ex is KeyNotFoundException)
            return (404, "Resource not found.", null);
        if (ex is ArgumentException)
            return (400, ex.Message, null);
        // قاعدة بزنس اتخرقت: رسالتها مكتوبة لتتعرض للمستخدم عمداً (بخلاف رسائل الأخطاء البرمجية)
        if (ex is DomainException)
            return (409, ex.Message, null);

        // InvalidOperationException (و أي Exception تانية) لسه 500: .NET بترميها لأخطاء برمجية حقيقية،
        // ومش هنخبي Bugs ورا 409. قواعد البزنس بتستخدم DomainException.
        return (500, "An unexpected error occurred. Please try again later.", null);
    }
}
