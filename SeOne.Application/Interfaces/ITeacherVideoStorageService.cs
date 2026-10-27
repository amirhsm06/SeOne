namespace SeOne.Application.Interfaces;

public interface ITeacherVideoStorageService
{
    bool IsConfigured { get; }

    Task<string> UploadAsync(
        Guid teacherId,
        Stream content,
        string fileName,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid teacherId,
        string publicUrl,
        CancellationToken cancellationToken = default);
}