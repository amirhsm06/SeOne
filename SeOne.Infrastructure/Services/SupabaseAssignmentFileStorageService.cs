using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public sealed class SupabaseAssignmentFileStorageService : IAssignmentFileStorageService
{
    private const long MaximumFileSizeBytes = 20 * 1024 * 1024;
    private const int DownloadUrlLifetimeSeconds = 300;
    private static readonly IReadOnlyDictionary<string, string> AllowedFileTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly string? _bucket;
    private readonly string _imageBucket;

    public SupabaseAssignmentFileStorageService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["SUPABASE_URL"]?.TrimEnd('/');
        _serviceKey = configuration["SUPABASE_SECRET_KEY"]
            ?? configuration["SUPABASE_SERVICE_ROLE_KEY"];
        _bucket = configuration["SUPABASE_ASSIGNMENT_STORAGE_BUCKET"];
        _imageBucket = configuration["SUPABASE_STORAGE_BUCKET"] ?? "seone-images";
    }

    // Expose lower-level signing and deletion utilities for messaging integration
    public async Task<string?> CreateSignedDownloadUrlForObjectAsync(string objectPath, int expiresInSeconds = DownloadUrlLifetimeSeconds, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        if (!TryNormalizeObjectPath(objectPath, out var normalizedPath) ||
            !string.Equals(objectPath, normalizedPath, StringComparison.Ordinal))
            return null;

        try
        {
            var url = $"{_supabaseUrl}/storage/v1/object/sign/{Uri.EscapeDataString(_bucket!)}/{EncodePath(normalizedPath)}";
            using var message = CreateAuthorizedRequest(HttpMethod.Post, url);
            message.Content = JsonContent.Create(new { expiresIn = expiresInSeconds });

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryGetProperty(document.RootElement, "signedURL", "signedUrl", out var signedUrlElement) ||
                signedUrlElement.ValueKind != JsonValueKind.String)
                return null;

            return MakeAbsoluteSignedUrl(signedUrlElement.GetString()!);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteObjectAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return false;

        if (!TryNormalizeObjectPath(objectPath, out var normalizedPath) ||
            !string.Equals(objectPath, normalizedPath, StringComparison.Ordinal))
            return false;

        try
        {
            var url = $"{_supabaseUrl}/storage/v1/object/{Uri.EscapeDataString(_bucket!)}/{EncodePath(normalizedPath)}";
            using var message = CreateAuthorizedRequest(HttpMethod.Delete, url);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_supabaseUrl) &&
        !string.IsNullOrWhiteSpace(_serviceKey) &&
        !string.IsNullOrWhiteSpace(_bucket) &&
        !string.Equals(_bucket, _imageBucket, StringComparison.Ordinal);

    public bool TryNormalizeObjectPath(string? reference, out string objectPath)
    {
        objectPath = string.Empty;
        if (!IsConfigured ||
            string.IsNullOrWhiteSpace(reference) ||
            reference.Length > 4096 ||
            HasUnsafePathSegments(reference))
            return false;

        IEnumerable<string> encodedSegments;
        if (Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            if (!Uri.TryCreate(_supabaseUrl, UriKind.Absolute, out var supabaseUri) ||
                !string.Equals(uri.Scheme, supabaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(uri.Host, supabaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
                uri.Port != supabaseUri.Port ||
                !string.IsNullOrEmpty(uri.UserInfo))
                return false;

            encodedSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var decoded = encodedSegments.Select(Uri.UnescapeDataString).ToArray();
            if (decoded.Length < 6 ||
                decoded[0] != "storage" || decoded[1] != "v1" || decoded[2] != "object" ||
                decoded[3] is not ("public" or "sign" or "authenticated") ||
                !string.Equals(decoded[4], _bucket, StringComparison.Ordinal))
                return false;

            encodedSegments = encodedSegments.Skip(5);
        }
        else
        {
            if (reference.Contains('?') || reference.Contains('#') || reference.Contains('\\'))
                return false;

            encodedSegments = reference.Split('/', StringSplitOptions.None);
        }

        var pathSegments = new List<string>();
        foreach (var encodedSegment in encodedSegments)
        {
            string segment;
            try
            {
                segment = Uri.UnescapeDataString(encodedSegment);
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.Contains('/') ||
                segment.Contains('\\') ||
                segment.Contains('%') ||
                segment.Contains('?') ||
                segment.Contains('#') ||
                segment.Any(char.IsControl))
                return false;

            pathSegments.Add(segment);
        }

        if (pathSegments.Count == 0)
            return false;

        objectPath = string.Join('/', pathSegments);
        return true;
    }

    public async Task<AssignmentUploadGrantResult> CreateUploadGrantAsync(
        Guid studentId,
        Guid assignmentId,
        AssignmentUploadGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);

        if (!TryValidateUpload(request, out var extension, out var contentType))
            return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.InvalidRequest);

        try
        {
            var bucketPolicy = await GetPrivateBucketPolicyAsync(cancellationToken);
            if (bucketPolicy is null)
                return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);

            if (request.Size > bucketPolicy.MaximumFileSizeBytes)
                return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.InvalidRequest);

            var objectPath = $"student-assignments/{studentId:D}/{assignmentId:D}/{Guid.NewGuid():N}{extension}";
            var url = $"{_supabaseUrl}/storage/v1/object/upload/sign/{Uri.EscapeDataString(_bucket!)}/{EncodePath(objectPath)}";
            using var message = CreateAuthorizedRequest(HttpMethod.Post, url);
            message.Content = JsonContent.Create(new { upsert = false });

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(tokenElement.GetString()))
                return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);

            return new AssignmentUploadGrantResult(
                AssignmentStorageOperationOutcome.Succeeded,
                new AssignmentUploadGrantDto(_bucket!, objectPath, tokenElement.GetString()!));
        }
        catch (HttpRequestException)
        {
            return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);
        }
        catch (JsonException)
        {
            return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AssignmentUploadGrantResult(AssignmentStorageOperationOutcome.Unavailable);
        }
    }

    public async Task<AssignmentSignedDownloadUrlResult> CreateSignedDownloadUrlAsync(
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);

        if (!TryNormalizeObjectPath(objectPath, out var normalizedPath) ||
            !string.Equals(objectPath, normalizedPath, StringComparison.Ordinal))
            return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.InvalidRequest);

        try
        {
            if (await GetPrivateBucketPolicyAsync(cancellationToken) is null)
                return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);

            var url = $"{_supabaseUrl}/storage/v1/object/sign/{Uri.EscapeDataString(_bucket!)}/{EncodePath(normalizedPath)}";
            using var message = CreateAuthorizedRequest(HttpMethod.Post, url);
            message.Content = JsonContent.Create(new { expiresIn = DownloadUrlLifetimeSeconds });

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryGetProperty(document.RootElement, "signedURL", "signedUrl", out var signedUrlElement) ||
                signedUrlElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(signedUrlElement.GetString()))
                return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);

            var signedUrl = MakeAbsoluteSignedUrl(signedUrlElement.GetString()!);
            if (signedUrl is null ||
                !Uri.TryCreate(signedUrl, UriKind.Absolute, out var signedUri) ||
                !signedUri.AbsolutePath.Contains("/storage/v1/object/sign/", StringComparison.Ordinal) ||
                !TryNormalizeObjectPath(signedUrl, out var signedPath) ||
                !string.Equals(signedPath, normalizedPath, StringComparison.Ordinal))
                return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);

            return new AssignmentSignedDownloadUrlResult(
                AssignmentStorageOperationOutcome.Succeeded,
                signedUrl);
        }
        catch (HttpRequestException)
        {
            return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);
        }
        catch (JsonException)
        {
            return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AssignmentSignedDownloadUrlResult(AssignmentStorageOperationOutcome.Unavailable);
        }
    }

    private async Task<AssignmentBucketPolicy?> GetPrivateBucketPolicyAsync(CancellationToken cancellationToken)
    {
        var url = $"{_supabaseUrl}/storage/v1/bucket/{Uri.EscapeDataString(_bucket!)}";
        using var message = CreateAuthorizedRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!TryGetProperty(root, "public", "public", out var publicElement) ||
            publicElement.ValueKind != JsonValueKind.False ||
            !TryGetProperty(root, "file_size_limit", "fileSizeLimit", out var sizeElement) ||
            sizeElement.ValueKind != JsonValueKind.Number ||
            !sizeElement.TryGetInt64(out var maximumFileSizeBytes) ||
            maximumFileSizeBytes is <= 0 or > MaximumFileSizeBytes ||
            !TryGetProperty(root, "allowed_mime_types", "allowedMimeTypes", out var mimeElement) ||
            mimeElement.ValueKind != JsonValueKind.Array)
            return null;

        var configuredMimeTypes = mimeElement.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredMimeTypes = AllowedFileTypes.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!configuredMimeTypes.SetEquals(requiredMimeTypes))
            return null;

        return new AssignmentBucketPolicy(maximumFileSizeBytes);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
        message.Headers.Add("apikey", _serviceKey);
        return message;
    }

    private string? MakeAbsoluteSignedUrl(string signedUrl)
    {
        if (Uri.TryCreate(signedUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (!Uri.TryCreate(_supabaseUrl, UriKind.Absolute, out var supabaseUri) ||
                !string.Equals(absoluteUri.Scheme, supabaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(absoluteUri.Host, supabaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
                absoluteUri.Port != supabaseUri.Port)
                return null;

            return absoluteUri.AbsoluteUri;
        }

        var relativePath = signedUrl.TrimStart('/');
        if (!relativePath.StartsWith("storage/v1/", StringComparison.Ordinal))
            relativePath = $"storage/v1/{relativePath}";
        return $"{_supabaseUrl}/{relativePath}";
    }

    private static bool TryValidateUpload(
        AssignmentUploadGrantRequest request,
        out string extension,
        out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;
        if (request is null ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            request.FileName.Length > 255 ||
            request.FileName.Contains('/') ||
            request.FileName.Contains('\\') ||
            request.FileName.Any(char.IsControl) ||
            request.Size <= 0 ||
            request.Size > MaximumFileSizeBytes)
            return false;

        extension = Path.GetExtension(request.FileName);

        if (!AllowedFileTypes.TryGetValue(extension, out var allowedContentType) ||
            !string.Equals(
                request.ContentType,
                allowedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        contentType = allowedContentType;
        return true;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string firstName,
        string secondName,
        out JsonElement value)
    {
        return element.TryGetProperty(firstName, out value) ||
               element.TryGetProperty(secondName, out value);
    }

    private static bool HasUnsafePathSegments(string reference)
    {
        var path = reference;
        var schemeSeparator = reference.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator >= 0)
        {
            var pathStart = reference.IndexOf('/', schemeSeparator + 3);
            if (pathStart < 0)
                return true;
            path = reference[pathStart..];
        }

        var queryStart = path.IndexOfAny(['?', '#']);
        if (queryStart >= 0)
            path = path[..queryStart];

        foreach (var rawSegment in path.Split('/'))
        {
            var segment = rawSegment;
            for (var decodeCount = 0; decodeCount < 2; decodeCount++)
            {
                try
                {
                    segment = Uri.UnescapeDataString(segment);
                }
                catch (UriFormatException)
                {
                    return true;
                }

                if (segment is "." or ".." || segment.Contains('/') || segment.Contains('\\'))
                    return true;
            }
        }

        return false;
    }

    private static string EncodePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private sealed record AssignmentBucketPolicy(long MaximumFileSizeBytes);
}
