namespace SponsorPulse.Application.Common.Models;

/// <summary>
/// Représente les paramètres de démonstration pour SponsorPulse.
/// Ces paramètres permettent de configurer l'organisation et les valeurs par défaut pour la démo.
/// </summary>
public class DemoSettings
{
    /// <summary>
    /// Nom de l'organisation (ex: HyperCore Gaming)
    /// </summary>
    public string? OrganizationName { get; set; } = "HyperCore Gaming";

    /// <summary>
    /// Logo de l'organisation en Base64
    /// </summary>
    public string? LogoBase64 { get; set; }

    /// <summary>
    /// Valeur par défaut pour l'Ad Value Equivalent (AVE) en euros
    /// Utilisé pour la démonstration Twitter Analytics
    /// </summary>
    public decimal DefaultAveValue { get; set; } = 12500m;

    /// <summary>
    /// Multiplicateur viral pour le calcul de portée
    /// Basé sur le ratio retweets/tweets
    /// </summary>
    public double ViralMultiplier { get; set; } = 1.84;

    /// <summary>
    /// Couleur primaire de la marque (format hexadécimal)
    /// Utilisée dans les rapports PDF et l'interface
    /// </summary>
    public string? PrimaryColor { get; set; } = "#CCC9DC";

    /// <summary>
    /// Couleur secondaire de la marque (format hexadécimal)
    /// Utilisée dans les rapports PDF et l'interface
    /// </summary>
    public string? SecondaryColor { get; set; } = "#475569";

    /// <summary>
    /// Date de dernière modification des paramètres
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
