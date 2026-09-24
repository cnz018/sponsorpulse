namespace SponsorPulse.Domain.Models.Analytics;

public sealed class StorytellingAnalysis
{
    public Guid EventId { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
