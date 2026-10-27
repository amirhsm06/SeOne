using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public enum SubmissionOperationOutcome
{
    Succeeded,
    InvalidRequest,
    AssignmentUnavailable,
    SubmissionNotFound,
    Conflict
}

public sealed record SubmissionOperationResult(
    SubmissionOperationOutcome Outcome,
    SubmissionDto? Submission = null);

public sealed class AssignmentUploadGrantRequest
{
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(127)]
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }
}

public sealed record AssignmentUploadGrantDto(
    string Bucket,
    string Path,
    string Token);

public enum AssignmentStorageOperationOutcome
{
    Succeeded,
    InvalidRequest,
    Unavailable
}

public sealed record AssignmentUploadGrantResult(
    AssignmentStorageOperationOutcome Outcome,
    AssignmentUploadGrantDto? Grant = null);

public sealed record AssignmentSignedDownloadUrlResult(
    AssignmentStorageOperationOutcome Outcome,
    string? Url = null);
