using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Files.Entities;

namespace AgFlow.Application.Modules.Files.Commands.ConfirmUpload;

// بيتنادى من الـ Frontend بعد ما يخلص يرفع الملف مباشرة على Azure (خطوة منفصلة عن طلب الرابط)
public record ConfirmFileUploadCommand(Guid TaskItemId, string FileName, string BlobPath) : IRequest<Guid>;

public class ConfirmFileUploadCommandHandler : IRequestHandler<ConfirmFileUploadCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ConfirmFileUploadCommandHandler(
        IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(ConfirmFileUploadCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        // === Fix أمني (IDOR): الـ BlobPath جاي من الـ Client، فمينفعش نصدقه ===
        // الـ Path اللي ولّدناه بنفسنا بيبدأ دايماً بـ WorkspaceId (AzureBlobStorageService)،
        // يبقى أي مسار مش بادئ بـ WorkspaceId بتاع المستخدم ده يتعتبر محاولة وصول لملف حد تاني
        if (!request.BlobPath.StartsWith($"{workspaceId}/", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid blob path.");

        var taskExists = await _dbContext.TaskItems
            .AnyAsync(t => t.Id == request.TaskItemId, cancellationToken);
        if (!taskExists)
            throw new KeyNotFoundException("Task not found.");

        var fileAsset = FileAsset.Create(workspaceId, request.TaskItemId, request.FileName, request.BlobPath);

        _dbContext.FileAssets.Add(fileAsset);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return fileAsset.Id;
    }
}
