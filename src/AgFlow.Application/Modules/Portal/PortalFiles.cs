using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Files.Entities;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Portal;

// ============================================================================
// ملفات الـ Portal: العميل يشوف الملفات اللي اتشاركت معاه (Status != NotRequired) في مشاريعه بس،
// ويقرر: موافقة أو طلب تعديل. كل Handler بيبدأ بـ PortalGuard، والـ ClientId من الـ Token.
// DTOs Allow-list: اسم الملف + اسم المشروع + الحالة. مفيش عنوان Task داخلي ولا أي Id داخلي زيادة.
// ============================================================================

public record PortalFileDto(
    Guid Id, string FileName, string ProjectName, string Status,
    int LatestVersion, DateTime LatestUploadedAt, DateTime? ReviewedAt, string? ReviewNote);

internal static class PortalFileScope
{
    // الملفات اللي تخص عميل واحد وبس اللي اتشاركت (مش الداخلية). النقطة الوحيدة اللي بنبني منها كل Query
    public static IQueryable<FileScopeRow> ForClient(IApplicationDbContext db, Guid clientId) =>
        db.FileAssets.AsNoTracking()
            .Where(f => f.ApprovalStatus != ApprovalStatus.NotRequired)
            .Join(db.TaskItems, f => f.TaskItemId, t => t.Id, (f, t) => new { f, t.TaskListId })
            .Join(db.TaskLists, x => x.TaskListId, l => l.Id, (x, l) => new { x.f, l.ProjectId })
            .Join(db.Projects, x => x.ProjectId, p => p.Id, (x, p) => new { x.f, p.Name, p.ClientId })
            .Where(x => x.ClientId == clientId)
            .Select(x => new FileScopeRow(x.f.Id, x.f.FileName, x.Name));

    internal record FileScopeRow(Guid FileId, string FileName, string ProjectName);
}

// ---------- القايمة ----------
public record ListPortalFilesQuery(ApprovalStatus? Status = null) : IRequest<List<PortalFileDto>>;

public class ListPortalFilesQueryHandler : IRequestHandler<ListPortalFilesQuery, List<PortalFileDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    public ListPortalFilesQueryHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    { _db = db; _portalUser = portalUser; }

    public async Task<List<PortalFileDto>> Handle(ListPortalFilesQuery request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);

        var ids = PortalFileScope.ForClient(_db, me.ClientId).Select(r => r.FileId);
        var projectNames = await PortalFileScope.ForClient(_db, me.ClientId)
            .ToDictionaryAsync(r => r.FileId, r => r.ProjectName, ct);

        var files = _db.FileAssets.AsNoTracking().Where(f => ids.Contains(f.Id));
        if (request.Status is { } st && st != ApprovalStatus.NotRequired)
            files = files.Where(f => f.ApprovalStatus == st);

        var rows = await files
            .OrderBy(f => f.ApprovalStatus != ApprovalStatus.Pending) // اللي مستني قرارك الأول
            .ThenBy(f => f.FileName).ThenBy(f => f.Id)
            .Take(200)
            .Select(f => new
            {
                f.Id, f.FileName, f.ApprovalStatus, f.ReviewedAt, f.ReviewNote,
                Latest = f.Versions.Max(v => v.VersionNumber),
                UploadedAt = f.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.UploadedAt).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r => new PortalFileDto(r.Id, r.FileName,
            projectNames.GetValueOrDefault(r.Id, string.Empty), r.ApprovalStatus.ToString(),
            r.Latest, r.UploadedAt, r.ReviewedAt, r.ReviewNote)).ToList();
    }
}

// ---------- التحميل ----------
public record PortalFileDownloadDto(string Url, int VersionNumber, string FileName);
public record GetPortalFileDownloadQuery(Guid FileId) : IRequest<PortalFileDownloadDto>;

public class GetPortalFileDownloadQueryHandler : IRequestHandler<GetPortalFileDownloadQuery, PortalFileDownloadDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    private readonly IFileStorageService _storage;
    public GetPortalFileDownloadQueryHandler(IApplicationDbContext db, ICurrentPortalUser portalUser, IFileStorageService storage)
    { _db = db; _portalUser = portalUser; _storage = storage; }

    public async Task<PortalFileDownloadDto> Handle(GetPortalFileDownloadQuery request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);

        // 404 لو الملف مش بتاع العميل ده أو لسه داخلي: مفيش فرق بين "مش موجود" و"مش ليك"
        var inScope = await PortalFileScope.ForClient(_db, me.ClientId).AnyAsync(r => r.FileId == request.FileId, ct);
        if (!inScope) throw new KeyNotFoundException("File not found.");

        var asset = await _db.FileAssets.AsNoTracking().Include(f => f.Versions)
            .FirstAsync(f => f.Id == request.FileId, ct);
        var version = asset.Versions.OrderByDescending(v => v.VersionNumber).First();

        // دفاع إضافي: المسار لازم يكون جوه Workspace الوكالة بتاعة العميل
        if (!version.BlobPath.StartsWith($"{me.WorkspaceId}/", StringComparison.Ordinal))
            throw new KeyNotFoundException("File not found.");

        return new PortalFileDownloadDto(_storage.GenerateDownloadUrl(version.BlobPath), version.VersionNumber, asset.FileName);
    }
}

// ---------- القرار: موافقة / طلب تعديل ----------
public record ApprovePortalFileCommand(Guid FileId, int VersionNumber) : IRequest;
public record RequestPortalFileRevisionCommand(Guid FileId, int VersionNumber, string Note) : IRequest;

public class RequestPortalFileRevisionCommandValidator : AbstractValidator<RequestPortalFileRevisionCommand>
{
    public RequestPortalFileRevisionCommandValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.VersionNumber).GreaterThan(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(FileAsset.MaxReviewNoteLength);
    }
}

internal static class PortalFileDecision
{
    public static async Task ApplyAsync(
        IApplicationDbContext db, PortalIdentity me, Guid fileId,
        Action<FileAsset> decide, Func<FileAsset, string, string> message, CancellationToken ct)
    {
        var inScope = await PortalFileScope.ForClient(db, me.ClientId).AnyAsync(r => r.FileId == fileId, ct);
        if (!inScope) throw new KeyNotFoundException("File not found.");

        var asset = await db.FileAssets.Include(f => f.Versions).FirstAsync(f => f.Id == fileId, ct);
        decide(asset); // الـ Domain بيفرض: Pending بس + النسخة الأحدث بس

        var clientName = await db.Clients.AsNoTracking().Where(c => c.Id == me.ClientId)
            .Select(c => c.CompanyName).FirstAsync(ct);

        // مين يتبلّغ: Owner/Admin/PM النشطين + المكلّفين بالـ Task. (سقف 50)
        var managers = db.WorkspaceMembers.AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active &&
                        (m.Role == WorkspaceRole.Owner || m.Role == WorkspaceRole.Admin || m.Role == WorkspaceRole.ProjectManager))
            .Select(m => m.UserId);
        var assignees = db.TaskAssignments.AsNoTracking()
            .Where(a => a.TaskItemId == asset.TaskItemId).Select(a => a.UserId);
        var recipients = await managers.Union(assignees).Take(50).ToListAsync(ct);

        // نص الإشعار نص عادي (مش HTML). الـ Frontend لازم يعرضه كـ Text، مش innerHTML:
        // الملاحظة جاية من عميل خارجي (Untrusted)
        var text = message(asset, clientName);
        foreach (var userId in recipients)
            db.Notifications.Add(AgFlow.Domain.Modules.Notifications.Entities.Notification.Create(asset.WorkspaceId, userId, text));

        await db.SaveChangesAsync(ct);
    }
}

public class ApprovePortalFileCommandHandler : IRequestHandler<ApprovePortalFileCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    public ApprovePortalFileCommandHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    { _db = db; _portalUser = portalUser; }

    public async Task Handle(ApprovePortalFileCommand request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);
        await PortalFileDecision.ApplyAsync(_db, me, request.FileId,
            a => a.Approve(me.ContactId, request.VersionNumber),
            (a, client) => $"{client} approved \"{a.FileName}\" (v{request.VersionNumber}).", ct);
    }
}

public class RequestPortalFileRevisionCommandHandler : IRequestHandler<RequestPortalFileRevisionCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    public RequestPortalFileRevisionCommandHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    { _db = db; _portalUser = portalUser; }

    public async Task Handle(RequestPortalFileRevisionCommand request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);
        await PortalFileDecision.ApplyAsync(_db, me, request.FileId,
            a => a.RequestRevision(me.ContactId, request.VersionNumber, request.Note),
            (a, client) =>
            {
                var note = request.Note.Trim();
                if (note.Length > 200) note = note[..200] + "…";
                return $"{client} requested changes on \"{a.FileName}\" (v{request.VersionNumber}): {note}";
            }, ct);
    }
}
