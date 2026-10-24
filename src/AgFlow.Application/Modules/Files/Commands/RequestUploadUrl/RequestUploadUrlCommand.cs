using MediatR;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Files.Commands.RequestUploadUrl;

public record RequestUploadUrlCommand(string FileName) : IRequest<RequestUploadUrlResult>;
public record RequestUploadUrlResult(string UploadUrl, string BlobPath);

public class RequestUploadUrlCommandHandler
    : IRequestHandler<RequestUploadUrlCommand, RequestUploadUrlResult>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;

    public RequestUploadUrlCommandHandler(
        IFileStorageService fileStorageService, ICurrentUserService currentUserService)
    {
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public Task<RequestUploadUrlResult> Handle(
        RequestUploadUrlCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var (uploadUrl, blobPath) = _fileStorageService.GenerateUploadUrl(workspaceId, request.FileName);
        return Task.FromResult(new RequestUploadUrlResult(uploadUrl, blobPath));
    }
}
