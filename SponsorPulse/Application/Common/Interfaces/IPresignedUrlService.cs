using SponsorPulse.Application.Common.Models;

namespace SponsorPulse.Application.Common.Interfaces;

public interface IPresignedUrlService
{
    Task<PresignedUrlResponse> GetPresignedUrlAsync(
        string fileName,
        string contentType,
        string eventSlug,
        CancellationToken cancellationToken = default
    );

    Task<UploadedMediaResult> UploadFileToPresignedUrlAsync(
        Stream fileStream,
        string fileName,
        string presignedUrl,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default
    );
}
