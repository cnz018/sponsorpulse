namespace SponsorPulse.Domain.Entities;

public class LinkedAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK vers ApplicationUser
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public PlatformType Platform { get; set; }

    // Les infos récupérées de la plateforme
    public string PlatformUserId { get; set; } = string.Empty;
    public string PlatformUsername { get; set; } = string.Empty;

    // Sécurité / Futurs besoins
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}
