using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces;

public interface ISupportFileStorageService
{
    bool IsSupportObjectPath(string? objectPath);

    Task<bool> UploadAsync(
        string objectPath,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<string?> CreateSignedDownloadUrlAsync(
        string objectPath,
        int expiresInSeconds = 300,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteObjectAsync(
        string objectPath,
        CancellationToken cancellationToken = default);
}