namespace SeOne.Application.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);
}