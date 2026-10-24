namespace AgFlow.Domain.Modules.Files.Entities;

public class FileVersion
{
    public Guid Id { get; private set; }
    public Guid FileAssetId { get; private set; }
    public string BlobPath { get; private set; } = string.Empty; // مسار الملف الفعلي في Azure Blob Storage
    public int VersionNumber { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private FileVersion() { }

    internal static FileVersion Create(Guid fileAssetId, string blobPath, int versionNumber)
    {
        return new FileVersion
        {
            Id = Guid.NewGuid(),
            FileAssetId = fileAssetId,
            BlobPath = blobPath,
            VersionNumber = versionNumber,
            UploadedAt = DateTime.UtcNow
        };
    }
}
