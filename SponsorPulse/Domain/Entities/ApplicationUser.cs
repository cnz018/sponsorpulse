namespace SponsorPulse.Domain.Entities;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    // Informations basiques
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Organisation (optionnel)
    public string? OrganizationName { get; set; }

    // Media (optionnel) - stocker des URLs ou Base64 selon l'implémentation
    public string? ProfilePhotoUrl { get; set; }
    public string? OrganizationLogoUrl { get; set; }

    // Relations
    public Settings? Settings { get; set; }
    public Dashboard? Dashboard { get; set; }
    public ICollection<Event> Events { get; set; } = [];
    public ICollection<TwitchAuthToken> TwitchAuthTokens { get; set; } = [];
    public ICollection<LinkedAccount> LinkedAccounts { get; set; } = [];
}
