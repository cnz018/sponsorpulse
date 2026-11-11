using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;

namespace SponsorPulse.Presentation.Services;

/// <summary>
/// Service client pour obtenir les URLs presignées de l'API minimale locale.
/// Disponible en Blazor Server (composants interactifs).
/// </summary>
public class PresignedUrlApiService(HttpClient httpClient, ILogger<PresignedUrlApiService> logger)
    : IPresignedUrlService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<PresignedUrlApiService> _logger = logger;
    private readonly string _apiBaseUrl = "/api/media/presigned-url";

    public async Task<PresignedUrlResponse> GetPresignedUrlAsync(
        string fileName,
        string contentType,
        string eventSlug,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(eventSlug))
            throw new ArgumentException("FileName and EventSlug are required.");

        try
        {
            var request = new PresignedUrlRequest(fileName, eventSlug, contentType);

            _logger.LogInformation($"Calling local API at {_apiBaseUrl} for file: {fileName}");

            var response = await _httpClient.PostAsJsonAsync(
                _apiBaseUrl,
                request,
                cancellationToken
            );

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = System.Text.Json.JsonSerializer.Deserialize<PresignedUrlResponse>(
                    jsonString,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    }
                );

                _logger.LogInformation($"Successfully obtained presigned URL for {fileName}");
                return result ?? throw new Exception("Empty response from API.");
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError($"API returned {response.StatusCode}: {errorContent}");
            throw new Exception(
                $"Failed to get presigned URL: {response.StatusCode} - {errorContent}"
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling API");
            throw new Exception($"HTTP error getting presigned URL: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting presigned URL");
            throw;
        }
    }

    public async Task<UploadedMediaResult> UploadFileToPresignedUrlAsync(
        Stream fileStream,
        string fileName,
        string presignedUrl,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default
    )
    {
        // This is a client-side operation, handled in Blazor component
        await Task.Delay(0, cancellationToken);
        return new UploadedMediaResult(presignedUrl, fileName, fileName, 0, "");
    }
}
