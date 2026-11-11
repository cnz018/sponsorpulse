using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;

namespace SponsorPulse.Infrastructure.Api.Extensions;

/// <summary>
/// Extension methods for mapping media presigned URL endpoints.
/// Follows Clean Architecture by separating endpoint configuration from Program.cs.
/// </summary>
public static class MediaPresignedUrlExtensions
{
    /// <summary>
    /// Maps the presigned URL endpoint to the application.
    /// </summary>
    /// <param name="app">The web application builder</param>
    /// <returns>The web application for further configuration</returns>
    public static WebApplication MapMediaPresignedUrlEndpoints(this WebApplication app)
    {
        // Create scope to resolve the handler
        var handler = new MediaPresignedUrlHandler();

        app.MapPost("/api/media/presigned-url", handler.HandlePresignedUrl)
            .WithName("GetPresignedUrl");

        return app;
    }
}

/// <summary>
/// Request handler for media presigned URL operations.
/// Handles the business logic of generating presigned URLs for S3/R2 uploads.
/// </summary>
public class MediaPresignedUrlHandler
{
    /// <summary>
    /// Handles POST requests to generate presigned URLs for S3/R2 uploads.
    /// Generates a unique filename with GUID and returns presigned URL + headers.
    /// </summary>
    /// <param name="request">The presigned URL request containing filename, event slug, and content type</param>
    /// <param name="mediaStorageService">The media storage service for R2 operations</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>The presigned URL response with URL and headers, or error response</returns>
    public async Task<IResult> HandlePresignedUrl(
        PresignedUrlRequest request,
        IMediaStorageService mediaStorageService,
        ILogger<MediaPresignedUrlHandler> logger,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Validate request
            if (
                request is null
                || string.IsNullOrWhiteSpace(request.FileName)
                || string.IsNullOrWhiteSpace(request.EventSlug)
            )
            {
                logger.LogWarning("Invalid request: missing FileName or EventSlug");
                return Results.BadRequest(new { error = "FileName and EventSlug are required." });
            }

            // Generate unique storage filename with GUID
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var fileId = Guid.NewGuid();
            var storageName = $"{request.EventSlug}-{fileId}{extension}";

            logger.LogInformation(
                "Generating presigned URL for file: {StorageName}, ContentType: {ContentType}",
                storageName,
                request.ContentType
            );

            // Generate presigned URL from R2 service
            var presigned = await mediaStorageService.GenerateUploadUrlAsync(
                storageName,
                request.ContentType,
                cancellationToken
            );

            // Build response
            var responseData = new PresignedUrlResponse(
                presigned.Url,
                presigned.Headers,
                storageName
            );

            logger.LogInformation(
                "Successfully generated presigned URL for {StorageName}",
                storageName
            );

            return Results.Ok(responseData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating presigned URL");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
