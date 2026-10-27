using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public sealed class SupabaseTeacherVideoStorageService
    : ITeacherVideoStorageService
{
    private const long MaxFileSizeBytes = 45_000_000L;

    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly string? _bucket;
    private readonly string _imageBucket;

    public SupabaseTeacherVideoStorageService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _supabaseUrl = configuration["SUPABASE_URL"]?.TrimEnd('/');

        _serviceKey =
            configuration["SUPABASE_SECRET_KEY"]
            ?? configuration["SUPABASE_SERVICE_ROLE_KEY"];

        _bucket = configuration["SUPABASE_TEACHER_VIDEO_STORAGE_BUCKET"];

        _imageBucket = configuration["SUPABASE_STORAGE_BUCKET"]
            ?? "seone-images";
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_supabaseUrl) &&
        !string.IsNullOrWhiteSpace(_serviceKey) &&
        !string.IsNullOrWhiteSpace(_bucket) &&
        !string.Equals(
            _bucket,
            _imageBucket,
            StringComparison.Ordinal);
    public async Task<bool> DeleteAsync(
    Guid teacherId,
    string publicUrl,
    CancellationToken cancellationToken = default)
    {
        if (!IsConfigured ||
            teacherId == Guid.Empty ||
            string.IsNullOrWhiteSpace(publicUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(_supabaseUrl, UriKind.Absolute, out var baseUri) ||
            !Uri.TryCreate(publicUrl, UriKind.Absolute, out var videoUri))
        {
            return false;
        }

        // Only allow URLs belonging to this configured Supabase instance.
        if (!string.Equals(
                videoUri.Scheme,
                baseUri.Scheme,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                videoUri.Host,
                baseUri.Host,
                StringComparison.OrdinalIgnoreCase) ||
            videoUri.Port != baseUri.Port ||
            !string.IsNullOrEmpty(videoUri.UserInfo) ||
            !string.IsNullOrEmpty(videoUri.Query) ||
            !string.IsNullOrEmpty(videoUri.Fragment))
        {
            return false;
        }

        string[] segments;

        try
        {
            segments = Uri.UnescapeDataString(videoUri.AbsolutePath)
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (UriFormatException)
        {
            return false;
        }

        // Expected URL:
        // /storage/v1/object/public/{bucket}/teacher-videos/{teacherId}/{guid}.{ext}
        if (segments.Length != 8 ||
            !string.Equals(segments[0], "storage", StringComparison.Ordinal) ||
            !string.Equals(segments[1], "v1", StringComparison.Ordinal) ||
            !string.Equals(segments[2], "object", StringComparison.Ordinal) ||
            !string.Equals(segments[3], "public", StringComparison.Ordinal) ||
            !string.Equals(segments[4], _bucket, StringComparison.Ordinal) ||
            !string.Equals(segments[5], "teacher-videos", StringComparison.Ordinal) ||
            !string.Equals(
                segments[6],
                teacherId.ToString("D"),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = segments[7];
        var extension = Path.GetExtension(fileName);
        var fileId = Path.GetFileNameWithoutExtension(fileName);

        if (Path.GetFileName(fileName) != fileName ||
            !Guid.TryParseExact(fileId, "N", out _) ||
            !(string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Reconstruct the path from validated segments rather than trusting
        // an arbitrary caller-supplied storage path.
        var objectPath = $"teacher-videos/{teacherId:D}/{fileName}";

        var encodedPath = string.Join(
            "/",
            objectPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var deleteUrl =
            $"{_supabaseUrl}/storage/v1/object/" +
            $"{Uri.EscapeDataString(_bucket!)}/{encodedPath}";

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            deleteUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _serviceKey);

        request.Headers.TryAddWithoutValidation("apikey", _serviceKey);

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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Treat an HttpClient timeout as a failed cleanup attempt.
            return false;
        }
    }

    public async Task<string> UploadAsync(
        Guid teacherId,
        Stream content,
        string fileName,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Teacher video storage is not configured.");

        if (teacherId == Guid.Empty)
            throw new InvalidOperationException("Invalid teacher ID.");

        if (content is null || !content.CanRead)
            throw new InvalidOperationException(
                "The video stream is unavailable.");

        if (contentLength <= 0 || contentLength > MaxFileSizeBytes)
            throw new InvalidOperationException(
                "Video size must be between 1 byte and 45 MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var expectedContentType = extension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => null
        };

        if (expectedContentType is null ||
            !string.Equals(
                expectedContentType,
                contentType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only MP4 and WebM videos with matching content types are allowed.");
        }

        var objectPath =
            $"teacher-videos/{teacherId:D}/{Guid.NewGuid():N}{extension}";

        var encodedPath = string.Join(
            "/",
            objectPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var uploadUrl =
            $"{_supabaseUrl}/storage/v1/object/" +
            $"{Uri.EscapeDataString(_bucket!)}/{encodedPath}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            uploadUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _serviceKey);

        request.Headers.TryAddWithoutValidation("apikey", _serviceKey);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");

        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue(expectedContentType);

        streamContent.Headers.ContentLength = contentLength;

        request.Content = streamContent;

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Supabase video upload failed with HTTP {(int)response.StatusCode}.");
        }

        return
            $"{_supabaseUrl}/storage/v1/object/public/" +
            $"{Uri.EscapeDataString(_bucket!)}/{encodedPath}";
    }
}