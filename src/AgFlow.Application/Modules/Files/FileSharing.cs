using System.Net;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Files.Entities;

namespace AgFlow.Application.Modules.Files.Sharing;

// ============================================================================
// الجانب الداخلي (الموظفين) لمشاركة الملفات مع العميل (FR-FM-2/3/4).
// القاعدة: الملف "داخلي" افتراضياً (NotRequired). مفيش حاجة بتوصل للعميل إلا لما موظف
// يعمل RequestApproval صراحةً. ده Allow-list مقصود، مش Deny-list.
// ============================================================================

public record FileAssetDto(
    Guid Id, Guid TaskItemId, string FileName, string ApprovalStatus,
    int LatestVersion, int VersionsCount, DateTime LatestUploadedAt,
    DateTime? ReviewedAt, string? ReviewNote);

// ---------- قايمة ملفات Task ----------
public record ListTaskFilesQuery(Guid TaskItemId) : IRequest<IReadOnlyList<FileAssetDto>>;

public class ListTaskFilesQueryHandler : IRequestHandler<ListTaskFilesQuery, IReadOnlyList<FileAssetDto>>
{
    private readonly IApplicationDbContext _db;
    public ListTaskFilesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FileAssetDto>> Handle(ListTaskFilesQuery request, CancellationToken ct)
    {
        var taskExists = await _db.TaskItems.AnyAsync(t => t.Id == request.TaskItemId, ct);
        if (!taskExists) throw new KeyNotFoundException("Task not found.");

        var rows = await _db.FileAssets.AsNoTracking()
            .Where(f => f.TaskItemId == request.TaskItemId)
            .OrderBy(f => f.FileName).ThenBy(f => f.Id)
            .Take(200)
            .Select(f => new
            {
                f.Id, f.TaskItemId, f.FileName, f.ApprovalStatus, f.ReviewedAt, f.ReviewNote,
                Count = f.Versions.Count,
                Latest = f.Versions.Max(v => v.VersionNumber),
                UploadedAt = f.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.UploadedAt).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r => new FileAssetDto(r.Id, r.TaskItemId, r.FileName, r.ApprovalStatus.ToString(),
            r.Latest, r.Count, r.UploadedAt, r.ReviewedAt, r.ReviewNote)).ToList();
    }
}

// ---------- نسخة جديدة ----------
public record AddFileVersionCommand(Guid FileAssetId, string BlobPath) : IRequest<int>;

public class AddFileVersionCommandValidator : AbstractValidator<AddFileVersionCommand>
{
    public AddFileVersionCommandValidator()
    {
        RuleFor(x => x.FileAssetId).NotEmpty();
        RuleFor(x => x.BlobPath).NotEmpty().MaximumLength(1024);
    }
}

public class AddFileVersionCommandHandler : IRequestHandler<AddFileVersionCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public AddFileVersionCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    { _db = db; _current = current; }

    public async Task<int> Handle(AddFileVersionCommand request, CancellationToken ct)
    {
        var workspaceId = _current.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        // نفس حماية ConfirmFileUpload: الـ BlobPath جاي من الـ Client فمانصدقوش
        if (!request.BlobPath.StartsWith($"{workspaceId}/", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid blob path.");

        var asset = await _db.FileAssets.Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == request.FileAssetId, ct)
            ?? throw new KeyNotFoundException("File not found.");

        asset.AddNewVersion(request.BlobPath);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) // Unique (FileAssetId, VersionNumber): اتنين رفعوا في نفس اللحظة
        {
            throw new AgFlow.Domain.Common.DomainException("Another version was uploaded at the same time. Please retry.");
        }
        return asset.LatestVersionNumber;
    }
}

// ---------- مشاركة مع العميل (طلب موافقة) ----------
public record RequestFileApprovalCommand(Guid FileAssetId) : IRequest;

public class RequestFileApprovalCommandHandler : IRequestHandler<RequestFileApprovalCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<RequestFileApprovalCommandHandler> _logger;

    public RequestFileApprovalCommandHandler(IApplicationDbContext db, IEmailService email,
        IConfiguration config, ILogger<RequestFileApprovalCommandHandler> logger)
    { _db = db; _email = email; _config = config; _logger = logger; }

    public async Task Handle(RequestFileApprovalCommand request, CancellationToken ct)
    {
        var asset = await _db.FileAssets.FirstOrDefaultAsync(f => f.Id == request.FileAssetId, ct)
            ?? throw new KeyNotFoundException("File not found.");

        // الملف → Task → TaskList → Project → Client. لازم يوصل لعميل نشط وإلا مفيش حد نشاركه معاه
        var target = await _db.TaskItems.AsNoTracking()
            .Where(t => t.Id == asset.TaskItemId)
            .Join(_db.TaskLists, t => t.TaskListId, l => l.Id, (t, l) => l)
            .Join(_db.Projects, l => l.ProjectId, p => p.Id, (l, p) => p.ClientId)
            .Join(_db.Clients, cid => cid, c => c.Id, (cid, c) => new { c.Id, c.CompanyName, c.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("The file's task is not linked to a client project.");

        if (target.Status != AgFlow.Domain.Modules.Clients.Entities.ClientStatus.Active)
            throw new AgFlow.Domain.Common.DomainException("Cannot share files with an archived client.");

        asset.RequestApproval();
        await _db.SaveChangesAsync(ct);

        // ---- إشعار بالإيميل لمستخدمي الـ Portal المفعّلين بس (best-effort: فشل الإيميل مايلغيش المشاركة) ----
        try
        {
            var recipients = await _db.PortalCredentials.AsNoTracking()
                .Where(c => c.ClientId == target.Id && c.IsEnabled && c.PasswordHash != null)
                .Select(c => c.Email).Take(20).ToListAsync(ct);
            if (recipients.Count == 0) return;

            var workspaceName = await _db.Workspaces.AsNoTracking()
                .Where(w => w.Id == asset.WorkspaceId).Select(w => w.Name).FirstAsync(ct);
            var baseUrl = (_config["App:PortalBaseUrl"] ?? _config["App:BaseUrl"] ?? string.Empty).TrimEnd('/');

            // SendGrid بيبعت الـ Body كـ HTML: أي نص جاي من مستخدم لازم يتعمله Encode (منع HTML/Phishing Injection)
            var body = $"<p>{WebUtility.HtmlEncode(workspaceName)} shared a file for your review: " +
                       $"<strong>{WebUtility.HtmlEncode(asset.FileName)}</strong>.</p>" +
                       (baseUrl.Length > 0 ? $"<p><a href=\"{WebUtility.HtmlEncode(baseUrl)}/portal\">Open your portal</a></p>" : "");

            foreach (var to in recipients)
                await _email.SendAsync(to, $"File ready for review: {asset.FileName}", body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send approval-request email for file {FileId}", asset.Id);
        }
    }
}

// ---------- رابط تحميل (موظف) ----------
public record FileDownloadDto(string Url, int VersionNumber, string FileName);
public record GetFileDownloadUrlQuery(Guid FileAssetId, int? VersionNumber = null) : IRequest<FileDownloadDto>;

public class GetFileDownloadUrlQueryHandler : IRequestHandler<GetFileDownloadUrlQuery, FileDownloadDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _current;
    public GetFileDownloadUrlQueryHandler(IApplicationDbContext db, IFileStorageService storage, ICurrentUserService current)
    { _db = db; _storage = storage; _current = current; }

    public async Task<FileDownloadDto> Handle(GetFileDownloadUrlQuery request, CancellationToken ct)
    {
        var workspaceId = _current.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var asset = await _db.FileAssets.AsNoTracking().Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == request.FileAssetId, ct)
            ?? throw new KeyNotFoundException("File not found.");

        var version = request.VersionNumber is int n
            ? asset.Versions.FirstOrDefault(v => v.VersionNumber == n)
            : asset.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (version is null) throw new KeyNotFoundException("Version not found.");

        // دفاع إضافي: مانولّدش SAS لمسار برا الـ Workspace حتى لو الداتا اتبوظت
        if (!version.BlobPath.StartsWith($"{workspaceId}/", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid blob path.");

        return new FileDownloadDto(_storage.GenerateDownloadUrl(version.BlobPath), version.VersionNumber, asset.FileName);
    }
}
