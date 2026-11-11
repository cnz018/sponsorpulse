using System.Net.Http.Headers;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Infrastructure.CloudflareR2;

namespace SponsorPulse.Infrastructure.Services;

public class MediaStorageService(
    IOptions<R2Settings> r2Options,
    IHttpClientFactory httpClientFactory,
    IR2ClientFactory r2ClientFactory
) : IMediaStorageService
{
    private readonly R2Settings _settings = r2Options.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IR2ClientFactory _r2ClientFactory = r2ClientFactory;

    public async Task<UploadedMediaResult> UploadMediaAsync(
        IBrowserFile file,
        string eventSlug,
        CancellationToken cancellationToken = default
    )
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        var fileId = Guid.NewGuid();
        var storageName = $"{eventSlug}-{fileId}{extension}";
        var presigned = await GenerateUploadUrlAsync(storageName, file.ContentType);

        // Upload file to presigned URL with retry logic
        var uploadSuccess = await UploadWithRetryAsync(file, presigned, cancellationToken);
        if (!uploadSuccess)
        {
            throw new Exception($"Upload failed for {file.Name} after retries.");
        }

        return new UploadedMediaResult(
            presigned.Url,
            storageName,
            file.Name,
            file.Size,
            file.ContentType
        );
    }

    public Task DeleteMediaAsync(
        string storageFileName,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: Implement delete with R2 API if needed
        return Task.CompletedTask;
    }

    public Task<PresignedUploadResult> GenerateUploadUrlAsync(
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default
    )
    {
        using var s3Client = _r2ClientFactory.CreateS3Client();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddMinutes(_settings.ExpirationTimeInMinute),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
        };

        var presignedUrl = s3Client.GetPreSignedURL(request);

        return Task.FromResult(new PresignedUploadResult(presignedUrl, _settings.UploadHeaders));
    }

    private async Task<bool> UploadWithRetryAsync(
        IBrowserFile file,
        PresignedUploadResult presigned,
        CancellationToken cancellationToken
    )
    {
        int maxRetries = 3;
        int delay = 500;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                using var stream = file.OpenReadStream(cancellationToken: cancellationToken);
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                foreach (var header in presigned.Headers)
                {
                    content.Headers.Add(header.Key, header.Value);
                }

                var response = await client.PutAsync(presigned.Url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch when (attempt < maxRetries)
            {
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
        }
        return false;
    }
}
