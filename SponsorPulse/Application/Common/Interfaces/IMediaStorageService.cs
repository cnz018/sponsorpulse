using Microsoft.AspNetCore.Components.Forms;
using SponsorPulse.Application.Common.Models;

namespace SponsorPulse.Application.Common.Interfaces;

public record UploadedMediaResult(
    string Url,
    string StorageFileName,
    string OriginalFileName,
    long Size,
    string ContentType
);

public interface IMediaStorageService
{
    Task<UploadedMediaResult> UploadMediaAsync(
        IBrowserFile file,
        string eventSlug,
        CancellationToken cancellationToken = default
    );
    Task DeleteMediaAsync(string storageFileName, CancellationToken cancellationToken = default);
    Task<PresignedUploadResult> GenerateUploadUrlAsync(
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default
    );
}
