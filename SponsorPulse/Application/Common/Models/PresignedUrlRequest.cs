namespace SponsorPulse.Application.Common.Models;

public record PresignedUrlRequest(string FileName, string ContentType, string EventSlug);

public record PresignedUrlResponse(string Url, Dictionary<string, string> Headers, string FileName);
