using SponsorPulse.Domain.Enums;

namespace SponsorPulse.Domain.Models;

public class SocialFetchResult
{
    public SocialPlatform Platform { get; init; }
    public string Query { get; init; } = string.Empty;
    public DateRange Period { get; init; } = new();
    public int TotalCount { get; init; }
    public List<SocialPost> Posts { get; init; } = [];
}