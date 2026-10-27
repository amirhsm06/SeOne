using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public sealed class SupabaseSupportFileStorageService
    : ISupportFileStorageService
{
    private const long MaximumFileSizeBytes = 10_000_000;
    private const int MaximumSignedUrlLifetimeSeconds = 3600;

    private static readonly IReadOnlyDictionary<string, string> AllowedMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain"
        };

    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly string? _bucket;
    private readonly string? _messagingBucket;
    private readonly string? _assignmentBucket;
    private readonly string? _teacherVideoBucket;
    private readonly string _imageBucket;

    public SupabaseSupportFileStorageService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _supabaseUrl = configuration["SUPABASE_URL"]?.TrimEnd('/');

        _serviceKey =
            configuration["SUPABASE_SECRET_KEY"]
            ?? configuration["SUPABASE_SERVICE_ROLE_KEY"];

        _bucket = configuration["SUPABASE_SUPPORT_STORAGE_BUCKET"]?.Trim();

        _messagingBucket =
            configuration["SUPABASE_MESSAGING_STORAGE_BUCKET"]?.Trim();

        _assignmentBucket =
            configuration["SUPABASE_ASSIGNMENT_STORAGE_BUCKET"]?.Trim();

        _teacherVideoBucket =
            configuration["SUPABASE_TEACHER_VIDEO_STORAGE_BUCKET"]?.Trim();

        _imageBucket =
            configuration["SUPABASE_STORAGE_BUCKET"]?.Trim()
            ?? "seone-images";
    }

    private bool IsConfigured
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_supabaseUrl) ||
                string.IsNullOrWhiteSpace(_serviceKey) ||
                string.IsNullOrWhiteSpace(_bucket) ||
                _bucket.Contains('/') ||
                string.Equals(_bucket, _messagingBucket,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_bucket, _assignmentBucket,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_bucket, _teacherVideoBucket,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_bucket, _imageBucket,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Uri.TryCreate(
                       _supabaseUrl,
                       UriKind.Absolute,
                       out var uri)
                   && uri.Scheme == Uri.UriSchemeHttps
                   && string.IsNullOrEmpty(uri.UserInfo)
                   && string.IsNullOrEmpty(uri.Query)
                   && string.IsNullOrEmpty(uri.Fragment);
        }
    }

    public bool IsSupportObjectPath(string? objectPath)
    {
        return TryValidateObjectPath(
            objectPath,
            out _,
            out _,
            out _);
    }

    public async Task<bool> UploadAsync(
        string objectPath,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateObjectPath(
                objectPath,
                out var normalizedPath,
                out _,
                out var expectedMimeType) ||
            content is null ||
            !content.CanRead ||
            contentLength <= 0 ||
            contentLength > MaximumFileSizeBytes ||
            !string.Equals(
                contentType,
                expectedMimeType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!await IsPrivateBucketReadyAsync(
                expectedMimeType,
                cancellationToken))
        {
            return false;
        }

        var url =
            $"{_supabaseUrl}/storage/v1/object/" +
            $"{Uri.EscapeDataString(_bucket!)}/" +
            EncodePath(normalizedPath);

        using var request = CreateAuthorizedRequest(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");

        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(expectedMimeType);
        fileContent.Headers.ContentLength = contentLength;
        request.Content = fileContent;

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<string?> CreateSignedDownloadUrlAsync(
        string objectPath,
        int expiresInSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateObjectPath(
                objectPath,
                out var normalizedPath,
                out _,
                out var expectedMimeType) ||
            expiresInSeconds < 1 ||
            expiresInSeconds > MaximumSignedUrlLifetimeSeconds)
        {
            return null;
        }

        if (!await IsPrivateBucketReadyAsync(
                expectedMimeType,
                cancellationToken))
        {
            return null;
        }

        var url =
            $"{_supabaseUrl}/storage/v1/object/sign/" +
            $"{Uri.EscapeDataString(_bucket!)}/" +
            EncodePath(normalizedPath);

        using var request = CreateAuthorizedRequest(HttpMethod.Post, url);
        request.Content = JsonContent.Create(
            new { expiresIn = expiresInSeconds });

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(
                cancellationToken);

            using var document = JsonDocument.Parse(json);

            if (!TryGetJsonProperty(
                    document.RootElement,
                    "signedURL",
                    out var signedUrlElement) &&
                !TryGetJsonProperty(
                    document.RootElement,
                    "signedUrl",
                    out signedUrlElement))
            {
                return null;
            }

            if (signedUrlElement.ValueKind != JsonValueKind.String)
                return null;

            return ValidateSignedUrl(
                signedUrlElement.GetString(),
                normalizedPath);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<bool> DeleteObjectAsync(
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateObjectPath(
                objectPath,
                out var normalizedPath,
                out _,
                out var expectedMimeType))
        {
            return false;
        }

        if (!await IsPrivateBucketReadyAsync(
                expectedMimeType,
                cancellationToken))
        {
            return false;
        }

        var url =
            $"{_supabaseUrl}/storage/v1/object/" +
            Uri.EscapeDataString(_bucket!);

        using var request = CreateAuthorizedRequest(HttpMethod.Delete, url);
        request.Content = JsonContent.Create(
            new { prefixes = new[] { normalizedPath } });

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<bool> IsPrivateBucketReadyAsync(
        string expectedMimeType,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            return false;

        var url =
            $"{_supabaseUrl}/storage/v1/bucket/" +
            Uri.EscapeDataString(_bucket!);

        using var request = CreateAuthorizedRequest(HttpMethod.Get, url);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(
                cancellationToken);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!TryGetJsonProperty(root, "public", out var publicElement) ||
                publicElement.ValueKind != JsonValueKind.False)
            {
                return false;
            }

            if (!TryGetJsonProperty(
                    root,
                    "file_size_limit",
                    out var sizeElement) &&
                !TryGetJsonProperty(
                    root,
                    "fileSizeLimit",
                    out sizeElement))
            {
                return false;
            }

            if (sizeElement.ValueKind != JsonValueKind.Number ||
                !sizeElement.TryGetInt64(out var bucketLimit) ||
                bucketLimit <= 0 ||
                bucketLimit > MaximumFileSizeBytes)
            {
                return false;
            }

            if (!TryGetJsonProperty(
                    root,
                    "allowed_mime_types",
                    out var mimeElement) &&
                !TryGetJsonProperty(
                    root,
                    "allowedMimeTypes",
                    out mimeElement))
            {
                return false;
            }

            if (mimeElement.ValueKind != JsonValueKind.Array)
                return false;

            return mimeElement.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String &&
                string.Equals(
                    item.GetString(),
                    expectedMimeType,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private string? ValidateSignedUrl(
        string? value,
        string expectedObjectPath)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(_supabaseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        Uri signedUri;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            signedUri = absoluteUri;
        }
        else
        {
            var relative = value.TrimStart('/');

            if (relative.StartsWith(
                    "object/sign/",
                    StringComparison.Ordinal))
            {
                relative = $"storage/v1/{relative}";
            }

            if (!relative.StartsWith(
                    "storage/v1/object/sign/",
                    StringComparison.Ordinal))
            {
                return null;
            }

            signedUri = new Uri(
                new Uri(_supabaseUrl!.TrimEnd('/') + "/"),
                relative);
        }

        if (!string.Equals(
                signedUri.Scheme,
                baseUri.Scheme,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                signedUri.Host,
                baseUri.Host,
                StringComparison.OrdinalIgnoreCase) ||
            signedUri.Port != baseUri.Port ||
            !string.IsNullOrEmpty(signedUri.UserInfo) ||
            !string.IsNullOrEmpty(signedUri.Fragment) ||
            !signedUri.Query.Contains(
                "token=",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = signedUri.AbsolutePath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 6 ||
            segments[0] != "storage" ||
            segments[1] != "v1" ||
            segments[2] != "object" ||
            segments[3] != "sign" ||
            !string.Equals(
                Uri.UnescapeDataString(segments[4]),
                _bucket,
                StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var returnedPath = string.Join(
                '/',
                segments.Skip(5).Select(Uri.UnescapeDataString));

            if (!string.Equals(
                    returnedPath,
                    expectedObjectPath,
                    StringComparison.Ordinal))
            {
                return null;
            }
        }
        catch (UriFormatException)
        {
            return null;
        }

        return signedUri.AbsoluteUri;
    }

    private static bool TryValidateObjectPath(
        string? objectPath,
        out string normalizedPath,
        out string extension,
        out string expectedMimeType)
    {
        normalizedPath = string.Empty;
        extension = string.Empty;
        expectedMimeType = string.Empty;

        if (string.IsNullOrWhiteSpace(objectPath) ||
            objectPath.Length > 512 ||
            objectPath.Contains('\\') ||
            objectPath.Contains('?') ||
            objectPath.Contains('#') ||
            objectPath.Contains('%') ||
            objectPath.Any(char.IsControl))
        {
            return false;
        }

        var segments = objectPath.Split('/', StringSplitOptions.None);

        if (segments.Length != 4 ||
            segments[0] != "support" ||
            !Guid.TryParseExact(segments[1], "D", out var ticketId) ||
            !Guid.TryParseExact(segments[2], "D", out var messageId) ||
            !string.Equals(
                segments[1],
                ticketId.ToString("D"),
                StringComparison.Ordinal) ||
            !string.Equals(
                segments[2],
                messageId.ToString("D"),
                StringComparison.Ordinal))
        {
            return false;
        }

        var fileName = segments[3];
        extension = Path.GetExtension(fileName);

        if (!AllowedMimeTypes.TryGetValue(extension, out var mimeType) ||
            string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        expectedMimeType = mimeType;

        var fileId = fileName[..^extension.Length];

        if (!Guid.TryParseExact(fileId, "N", out var parsedFileId) ||
            !string.Equals(
                fileId,
                parsedFileId.ToString("N"),
                StringComparison.Ordinal))
        {
            return false;
        }

        extension = extension.ToLowerInvariant();

        normalizedPath =
            $"support/{ticketId:D}/{messageId:D}/" +
            $"{parsedFileId:N}{extension}";

        return string.Equals(
            objectPath,
            normalizedPath,
            StringComparison.Ordinal);
    }

    private HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _serviceKey);
        request.Headers.TryAddWithoutValidation("apikey", _serviceKey);

        return request;
    }

    private static string EncodePath(string path)
    {
        return string.Join(
            '/',
            path.Split('/').Select(Uri.EscapeDataString));
    }

    private static bool TryGetJsonProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}