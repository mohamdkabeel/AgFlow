using System.Text.Json.Serialization;
using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Serilog;
using AgFlow.API;
using AgFlow.API.Middleware;
using AgFlow.Application;
using AgFlow.Application.Modules.Billing.Jobs;
using AgFlow.Application.Modules.Subscriptions.Jobs;
using AgFlow.Infrastructure;
using AgFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------- Services ----------
builder.Services.AddControllers()
    // الـ Enums (Role, Status...) تتبعت وتتقري كنص ("Admin") مش رقم (1): أوضح للـ Frontend وأأمن لو غيّرنا ترتيب الـ Enum
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();           // CurrentUserService بيحتاجه
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthRateLimiting();
builder.Services.AddAuthorization();                 // IAuthorizationService (TaskAssignment) محتاجه

builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
}).AddMvc().AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";
    o.SubstituteApiVersionInUrl = true; // Swagger مايطلبش منك تكتب الإصدار بإيدك
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "AgFlow API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "الصق الـ Access Token هنا (من غير كلمة Bearer)"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// CORS: الـ Origins من الإعدادات، مفيش "AllowAnyOrigin". لو الإعداد فاضي = ولا Origin مسموح (Fail Closed)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<AgFlowDbContext>("database");

// ورا Azure App Service/Load Balancer: من غير ده كل المستخدمين بنفس الـ IP بتاع الـ Proxy، فالـ Rate Limit بتاع الـ Login
// بيتشارك بين الكل (10 محاولات في الدقيقة لكل العالم). تحذير: ده بيثق في X-Forwarded-For، فالتطبيق لازم
// يكون متاح بس من ورا الـ Proxy، وإلا أي حد يزوّر الـ Header ويتخطى الـ Rate Limit
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// ---------- Pipeline (الترتيب مهم) ----------
app.UseForwardedHeaders();                         // أول حاجة: الـ IP الحقيقي لباقي الـ Middleware
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();  // بدري عشان يمسك أخطاء كل اللي بعده

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Hangfire Dashboard بيعرض Jobs كل الـ Workspaces (مش Tenant-aware): Development بس، ومن الجهاز نفسه (الافتراضي)
    app.UseHangfireDashboard("/hangfire");
}

app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<LogContextMiddleware>();          // بعد Authentication: محتاج UserId/WorkspaceId
app.UseMiddleware<SubscriptionStateMiddleware>();   // بعد Authentication: بيقرا WorkspaceId من الـ Token

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// ---------- Recurring Jobs (UTC) ----------
// AddOrUpdate بنفس الـ Id: إعادة تشغيل التطبيق مابتكررش الـ Job
using (var scope = app.Services.CreateScope())
{
    var jobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var utc = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

    // أول كل شهر 03:00: فواتير الشهر اللي فات. آمن لو اتنفذ تاني (Idempotent)
    jobs.AddOrUpdate<RecurringInvoiceJob>("recurring-invoices", j => j.RunAsync(), "0 3 1 * *", utc);
    // يومياً 06:00: تنبيه الـ Trial اللي هيخلص بعد 2-3 أيام
    jobs.AddOrUpdate<TrialEndingReminderJob>("trial-ending-reminders", j => j.RunAsync(), "0 6 * * *", utc);
}

app.Run();
