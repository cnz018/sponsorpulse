namespace SponsorPulse.Domain.Entities;

public class Settings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK vers ApplicationUser
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Exemple de paramètres stockés en base (adapter selon besoins)
    public bool ReceiveEmails { get; set; } = true;
    public string? PreferredLanguage { get; set; } = "en";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
