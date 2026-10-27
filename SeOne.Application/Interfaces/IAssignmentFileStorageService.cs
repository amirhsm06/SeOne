using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IAssignmentFileStorageService
{
    bool IsConfigured { get; }

    bool TryNormalizeObjectPath(string? reference, out string objectPath);

    Task<AssignmentUploadGrantResult> CreateUploadGrantAsync(
        Guid studentId,
        Guid assignmentId,
        AssignmentUploadGrantRequest request,
        CancellationToken cancellationToken = default);

    Task<AssignmentSignedDownloadUrlResult> CreateSignedDownloadUrlAsync(
        string objectPath,
        CancellationToken cancellationToken = default);
}
