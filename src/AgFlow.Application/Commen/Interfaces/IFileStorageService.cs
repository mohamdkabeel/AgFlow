namespace AgFlow.Application.Common.Interfaces;

public interface IFileStorageService
{
    // بيرجع رابط مؤقت (SAS Token) المتصفح يرفع عليه الملف مباشرة،
    // بالإضافة للـ "مسار" النهائي اللي هنخزنه في FileVersion.BlobPath
    (string UploadUrl, string BlobPath) GenerateUploadUrl(Guid workspaceId, string fileName);

    // بيرجع رابط مؤقت للتحميل (للـ Client Portal مثلاً، FR-FM-3)
    string GenerateDownloadUrl(string blobPath);
}
