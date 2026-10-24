using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Infrastructure.Storage;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureBlobStorage:ConnectionString"];
        var containerName = configuration["AzureBlobStorage:ContainerName"];
        _containerClient = new BlobContainerClient(connectionString, containerName);
    }

    public (string UploadUrl, string BlobPath) GenerateUploadUrl(Guid workspaceId, string fileName)
    {
        // عزل الملفات على مستوى الـ Path نفسه بالـ WorkspaceId (Phase 6, Step 4)
        var blobPath = $"{workspaceId}/{Guid.NewGuid()}-{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15) // رابط مؤقت جداً
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var uploadUrl = blobClient.GenerateSasUri(sasBuilder).ToString();
        return (uploadUrl, blobPath);
    }

    public string GenerateDownloadUrl(string blobPath)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }
}
