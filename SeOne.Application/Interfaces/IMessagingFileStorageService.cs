using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces;

public interface IMessagingFileStorageService
{
    /// <summary>
    /// Upload a file to the private messaging bucket.
    /// The object path must follow the server-generated messaging path format.
    /// The storage operation takes ownership of the stream and disposes it.
    /// Returns false if validation or storage fails.
    /// </summary>
    Task<bool> UploadAsync(
        string objectPath,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a short-lived signed download URL for a messaging object.
    /// Returns null if the path is invalid or signing fails.
    /// </summary>
    Task<string?> CreateSignedDownloadUrlAsync(
        string objectPath,
        int expiresInSeconds = 300,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a messaging object.
    /// Returns true when deletion succeeds.
    /// </summary>
    Task<bool> DeleteObjectAsync(
        string objectPath,
        CancellationToken cancellationToken = default);
}