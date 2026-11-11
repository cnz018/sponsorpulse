namespace SponsorPulse.Domain.Entities;

public class Dashboard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK vers ApplicationUser
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Stocker des métadonnées ou données calculées pour le dashboard
    // Ici un champ JSON générique pour conserver un état si besoin
    public string? DataJson { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
