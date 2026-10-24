using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Files.Entities;

public enum ApprovalStatus
{
    NotRequired,        // ملف داخلي: العميل مايشوفوش أبداً
    Pending,            // اتشارك مع العميل وبيستنى قراره
    Approved,
    RevisionRequested
}

public class FileAsset : ITenantScoped
{
    public const int MaxReviewNoteLength = 1000;

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TaskItemId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; private set; }

    // آخر قرار من العميل (موافقة أو طلب تعديل) ومين اللي اتخذه (Contact، مش User: العميل هوية منفصلة)
    public Guid? ReviewedByContactId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }

    private readonly List<FileVersion> _versions = new();
    public IReadOnlyCollection<FileVersion> Versions => _versions.AsReadOnly();

    public int LatestVersionNumber => _versions.Count == 0 ? 0 : _versions.Max(v => v.VersionNumber);

    private FileAsset() { }

    // FR-FM-1
    public static FileAsset Create(Guid workspaceId, Guid taskItemId, string fileName, string blobPath)
    {
        var asset = new FileAsset
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            TaskItemId = taskItemId,
            FileName = fileName,
            ApprovalStatus = ApprovalStatus.NotRequired
        };
        asset._versions.Add(FileVersion.Create(asset.Id, blobPath, versionNumber: 1));
        return asset;
    }

    // FR-FM-2: نسخة جديدة، مش استبدال.
    // أي قرار سابق (Pending/Approved/RevisionRequested) كان على نسخة قديمة، فبيتلغي:
    // مينفعش العميل يبقى "موافق" على ملف اتغير بعد موافقته. الوكالة لازم تطلب الموافقة من جديد
    public void AddNewVersion(string blobPath)
    {
        var next = LatestVersionNumber + 1;
        _versions.Add(FileVersion.Create(Id, blobPath, next));

        ApprovalStatus = ApprovalStatus.NotRequired;
        ReviewedByContactId = null;
        ReviewedAt = null;
        ReviewNote = null;
    }

    // FR-FM-4: الوكالة بتشارك النسخة الحالية مع العميل
    public void RequestApproval()
    {
        if (ApprovalStatus is ApprovalStatus.Pending)
            throw new DomainException("This file is already awaiting client approval.");
        if (ApprovalStatus is ApprovalStatus.Approved)
            throw new DomainException("This file is already approved. Upload a new version to request approval again.");

        ApprovalStatus = ApprovalStatus.Pending;
        ReviewedByContactId = null;
        ReviewedAt = null;
        ReviewNote = null;
    }

    // العميل بيوافق على "نسخة محددة" هو شافها. لو الوكالة رفعت نسخة أحدث وهو بيتفرج، القرار بيترفض
    public void Approve(Guid contactId, int versionNumber)
    {
        EnsureReviewable(versionNumber);
        ApprovalStatus = ApprovalStatus.Approved;
        ReviewedByContactId = contactId;
        ReviewedAt = DateTime.UtcNow;
        ReviewNote = null;
    }

    public void RequestRevision(Guid contactId, int versionNumber, string note)
    {
        EnsureReviewable(versionNumber);
        if (string.IsNullOrWhiteSpace(note))
            throw new DomainException("A note explaining the requested changes is required.");

        var trimmed = note.Trim();
        if (trimmed.Length > MaxReviewNoteLength)
            throw new DomainException($"Note cannot exceed {MaxReviewNoteLength} characters.");

        ApprovalStatus = ApprovalStatus.RevisionRequested;
        ReviewedByContactId = contactId;
        ReviewedAt = DateTime.UtcNow;
        ReviewNote = trimmed;
    }

    private void EnsureReviewable(int versionNumber)
    {
        if (ApprovalStatus != ApprovalStatus.Pending)
            throw new DomainException("This file is not awaiting approval.");
        if (versionNumber != LatestVersionNumber)
            throw new DomainException("A newer version of this file was uploaded. Please review the latest version.");
    }
}
