namespace SponsorPulse.Application.Common.Models;

public record PresignedUploadResult(string Url, Dictionary<string, string> Headers);
