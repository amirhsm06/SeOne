using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public class SupabaseImageStorageService : IImageStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _serviceRoleKey;
    private readonly string _bucket;

    public SupabaseImageStorageService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _supabaseUrl =
            configuration["SUPABASE_URL"]
            ?? throw new InvalidOperationException("SUPABASE_URL is not configured.");

        // Prefer explicit secret key; fall back to legacy service role key for compatibility
        _serviceRoleKey =
            configuration["SUPABASE_SECRET_KEY"]
            ?? configuration["SUPABASE_SERVICE_ROLE_KEY"]
            ?? throw new InvalidOperationException("SUPABASE secret key is not configured (SUPABASE_SECRET_KEY or SUPABASE_SERVICE_ROLE_KEY).");

        _bucket =
            configuration["SUPABASE_STORAGE_BUCKET"]
            ?? "seone-images";
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        if (content.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Only JPG, PNG and WEBP images are allowed.");

        var safeFolder = folder.Trim('/');

        var objectName = $"{Guid.NewGuid():N}{extension}";
        var objectPath = $"{safeFolder}/{objectName}";

        var encodedPath = string.Join(
            "/",
            objectPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var uploadUrl =
            $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/{Uri.EscapeDataString(_bucket)}/{encodedPath}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            uploadUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

        request.Headers.Add("apikey", _serviceRoleKey);

        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        request.Content = streamContent;

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Supabase Storage upload failed: {response.StatusCode} - {responseBody}");
        }

        return
            $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{Uri.EscapeDataString(_bucket)}/{encodedPath}";
    }
}