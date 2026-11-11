namespace SponsorPulse.Domain.Entities;

public record TwitchAuthToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string State { get; set; } = string.Empty;
    public string? TwitchUserId { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Scopes { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Optional link to our application user (if known)
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
}
