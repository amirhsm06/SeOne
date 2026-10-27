
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public sealed class SupabaseMessagingFileStorageService
    : IMessagingFileStorageService
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;
    private const int MaxSignedUrlLifetimeSeconds = 3600;

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
            [".txt"] = "text/plain",
            [".mp3"] = "audio/mpeg",
            [".m4a"] = "audio/mp4",
            [".wav"] = "audio/wav",
            [".mp4"] = "video/mp4",
            [".zip"] = "application/zip"
        };

    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly string? _bucket;
    private readonly string? _assignmentBucket;
    private readonly string _imageBucket;
    private readonly SemaphoreSlim _policyLock = new(1, 1);

    private MessagingBucketPolicy? _cachedPolicy;
    private DateTimeOffset _policyExpiresAt;

    public SupabaseMessagingFileStorageService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _supabaseUrl =
            configuration["SUPABASE_URL"]?.TrimEnd('/');

        _serviceKey =
            configuration["SUPABASE_SECRET_KEY"]
            ?? configuration["SUPABASE_SERVICE_ROLE_KEY"];

        // Deliberately do not fall back to another bucket.
        _bucket =
            configuration["SUPABASE_MESSAGING_STORAGE_BUCKET"]?.Trim();

        _assignmentBucket =
            configuration["SUPABASE_ASSIGNMENT_STORAGE_BUCKET"]?.Trim();

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
                string.Equals(_bucket, _assignmentBucket,
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
                   && (uri.Scheme == Uri.UriSchemeHttps ||
                       uri.Scheme == Uri.UriSchemeHttp)
                   && string.IsNullOrEmpty(uri.UserInfo)
                   && string.IsNullOrEmpty(uri.Query)
                   && string.IsNullOrEmpty(uri.Fragment);
        }
    }

    public async Task<bool> UploadAsync(
        string objectPath,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // This service owns and disposes the supplied stream.
        using var ownedContent = content;

        if (!IsConfigured ||
            !TryValidateObjectPath(objectPath, out var normalizedPath, out var extension) ||
            !string.Equals(objectPath, normalizedPath, StringComparison.Ordinal) ||
            contentLength <= 0 ||
            contentLength > MaxFileSizeBytes ||
            !AllowedMimeTypes.TryGetValue(extension, out var expectedMimeType) ||
            !IsCompatibleContentType(extension, contentType, expectedMimeType))
        {
            return false;
        }

        try
        {
            var policy = await GetPrivateBucketPolicyAsync(cancellationToken);

            if (policy is null ||
                contentLength > policy.MaximumFileSizeBytes ||
                !IsAllowedByBucketPolicy(expectedMimeType, policy.AllowedMimeTypes))
            {
                return false;
            }

            var url = BuildObjectUrl(normalizedPath);

            using var request = CreateAuthorizedRequest(HttpMethod.Post, url);

            var streamContent = new StreamContent(ownedContent);
            streamContent.Headers.ContentType =
                new MediaTypeHeaderValue(expectedMimeType);
            streamContent.Headers.ContentLength = contentLength;

            request.Content = streamContent;
            request.Headers.TryAddWithoutValidation("x-upsert", "false");

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
        catch (JsonException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<string?> CreateSignedDownloadUrlAsync(
        string objectPath,
        int expiresInSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured ||
            expiresInSeconds is < 1 or > MaxSignedUrlLifetimeSeconds ||
            !TryValidateObjectPath(objectPath, out var normalizedPath, out _) ||
            !string.Equals(objectPath, normalizedPath, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var policy = await GetPrivateBucketPolicyAsync(cancellationToken);

            if (policy is null)
                return null;

            var url =
                $"{_supabaseUrl}/storage/v1/object/sign/" +
                $"{Uri.EscapeDataString(_bucket!)}/" +
                EncodePath(normalizedPath);

            using var request = CreateAuthorizedRequest(HttpMethod.Post, url);
            request.Content = JsonContent.Create(
                new { expiresIn = expiresInSeconds });

            using var response =
                await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!TryGetProperty(
                    document.RootElement,
                    "signedURL",
                    "signedUrl",
                    out var signedUrlElement) ||
                signedUrlElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<bool> DeleteObjectAsync(
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured ||
            !TryValidateObjectPath(objectPath, out var normalizedPath, out _) ||
            !string.Equals(objectPath, normalizedPath, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var url = BuildObjectUrl(normalizedPath);

            using var request = CreateAuthorizedRequest(HttpMethod.Delete, url);

            using var response =
                await _httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<MessagingBucketPolicy?> GetPrivateBucketPolicyAsync(
        CancellationToken cancellationToken)
    {
        if (_cachedPolicy is not null &&
            DateTimeOffset.UtcNow < _policyExpiresAt)
        {
            return _cachedPolicy;
        }

        await _policyLock.WaitAsync(cancellationToken);

        try
        {
            if (_cachedPolicy is not null &&
                DateTimeOffset.UtcNow < _policyExpiresAt)
            {
                return _cachedPolicy;
            }

            var url =
                $"{_supabaseUrl}/storage/v1/bucket/" +
                Uri.EscapeDataString(_bucket!);

            using var request = CreateAuthorizedRequest(HttpMethod.Get, url);
            using var response =
                await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            var root = document.RootElement;

            if (!root.TryGetProperty("public", out var publicElement) ||
                publicElement.ValueKind != JsonValueKind.False ||
                !TryGetProperty(
                    root,
                    "file_size_limit",
                    "fileSizeLimit",
                    out var sizeElement) ||
                sizeElement.ValueKind != JsonValueKind.Number ||
                !sizeElement.TryGetInt64(out var maximumFileSizeBytes) ||
                maximumFileSizeBytes <= 0 ||
                maximumFileSizeBytes > MaxFileSizeBytes ||
                !TryGetProperty(
                    root,
                    "allowed_mime_types",
                    "allowedMimeTypes",
                    out var mimeElement) ||
                mimeElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var configuredMimeTypes = mimeElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Require bucket policy coverage for all supported extensions.
            foreach (var requiredType in AllowedMimeTypes.Values.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                if (!IsAllowedByBucketPolicy(requiredType, configuredMimeTypes))
                    return null;
            }

            _cachedPolicy = new MessagingBucketPolicy(
                maximumFileSizeBytes,
                configuredMimeTypes);

            _policyExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2);

            return _cachedPolicy;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            _policyLock.Release();
        }
    }

    private HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _serviceKey);
        request.Headers.Add("apikey", _serviceKey);

        return request;
    }

    private string BuildObjectUrl(string objectPath)
    {
        return
            $"{_supabaseUrl}/storage/v1/object/" +
            $"{Uri.EscapeDataString(_bucket!)}/" +
            EncodePath(objectPath);
    }

    private string? ValidateSignedUrl(
        string? value,
        string expectedObjectPath)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

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

            signedUri = new Uri(new Uri(_supabaseUrl!.TrimEnd('/') + "/"), relative);
        }

        var baseUri = new Uri(_supabaseUrl!);

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
            !signedUri.Query.Contains("token=", StringComparison.OrdinalIgnoreCase))
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
        out string extension)
    {
        normalizedPath = string.Empty;
        extension = string.Empty;

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
            segments[0] != "private-messaging" ||
            !Guid.TryParseExact(segments[1], "D", out var conversationId) ||
            !Guid.TryParseExact(segments[2], "D", out var messageId))
        {
            return false;
        }

        if (!string.Equals(
                segments[1],
                conversationId.ToString("D"),
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

        if (!AllowedMimeTypes.ContainsKey(extension))
            return false;

        var fileId = fileName[..^extension.Length];

        if (!Guid.TryParseExact(fileId, "N", out var parsedFileId) ||
            !string.Equals(
                fileId,
                parsedFileId.ToString("N"),
                StringComparison.Ordinal))
        {
            return false;
        }

        normalizedPath =
            $"private-messaging/{conversationId:D}/{messageId:D}/" +
            $"{parsedFileId:N}{extension}";

        return string.Equals(
            objectPath,
            normalizedPath,
            StringComparison.Ordinal);
    }

    private static bool IsCompatibleContentType(
        string extension,
        string? contentType,
        string expectedMimeType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return true;

        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            return false;

        var supplied = parsed.MediaType;

        if (string.IsNullOrWhiteSpace(supplied) ||
            supplied.Equals(
                "application/octet-stream",
                StringComparison.OrdinalIgnoreCase) ||
            supplied.Equals(expectedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" =>
                supplied.Equals("image/jpg", StringComparison.OrdinalIgnoreCase),

            ".m4a" =>
                supplied.Equals("audio/x-m4a", StringComparison.OrdinalIgnoreCase),

            ".wav" =>
                supplied.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase),

            ".zip" =>
                supplied.Equals("application/x-zip-compressed", StringComparison.OrdinalIgnoreCase),

            _ => false
        };
    }

    private static bool IsAllowedByBucketPolicy(
        string expectedMimeType,
        HashSet<string> configuredMimeTypes)
    {
        if (configuredMimeTypes.Contains(expectedMimeType))
            return true;

        var separator = expectedMimeType.IndexOf('/');

        if (separator <= 0)
            return false;

        var wildcard = expectedMimeType[..separator] + "/*";

        return configuredMimeTypes.Contains(wildcard) ||
               configuredMimeTypes.Contains("*/*");
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

    private static string EncodePath(string path)
    {
        return string.Join(
            '/',
            path.Split('/').Select(Uri.EscapeDataString));
    }

    private sealed record MessagingBucketPolicy(
        long MaximumFileSizeBytes,
        HashSet<string> AllowedMimeTypes);
}
