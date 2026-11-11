namespace SponsorPulse.Domain.Entities;

public record EventMedia(
    Guid Id,
    string OriginalFileName,
    string StorageFileName,
    string Url,
    long Size,
    string ContentType,
    DateTime UploadedAt
)
{
    public static EventMedia Create(
        string originalName,
        string storageName,
        string url,
        long size,
        string contentType
    )
    {
        return new EventMedia(
            Guid.NewGuid(),
            originalName,
            storageName,
            url,
            size,
            contentType,
            DateTime.UtcNow
        );
    }
}
