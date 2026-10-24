using Serilog.Context;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.API.Middleware;

// بيضيف WorkspaceId و UserId و CorrelationId تلقائياً لكل سطر Log يتكتب
// أثناء الـ Request ده — من غير ما أي كود تاني يحتاج يفتكر يكتبهم يدوي
public class LogContextMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        // CorrelationId: رقم واحد بيربط كل الـ Logs بتاعت Request واحد ببعض،
        // ولو الـ Frontend بعته في Header بنستخدمه، لو لأ بنولده
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("WorkspaceId", currentUser.WorkspaceId))
        using (LogContext.PushProperty("UserId", currentUser.UserId))
        {
            await _next(context);
        }
    }
}
