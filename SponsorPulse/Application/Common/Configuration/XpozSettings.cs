namespace SponsorPulse.Application.Common.Configuration;

/// <summary>
/// Configuration pour l'API Xpoz.ai
/// </summary>
public class XpozSettings
{
    /// <summary>
    /// Clé API pour l'authentification auprès de Xpoz.ai
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// URL de base de l'API Xpoz.ai
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Chemin de l'endpoint de recherche Twitter Xpoz.
    /// </summary>
    public string TwitterSearchPath { get; set; }

    /// <summary>
    /// Nom de l'en-tête API key pour les appels Xpoz.
    /// </summary>
    public string ApiKeyHeaderName { get; set; }

    /// <summary>
    /// Bucket par défaut pour les données Twitter
    /// </summary>
    public required string DefaultBucket { get; set; }

    /// <summary>
    /// Nombre de tentatives en cas d'erreur (par défaut 3)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Délai initial en millisecondes pour le retry exponentiel (par défaut 1000ms)
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Valeur du CPM (Coût Par Mille impressions) en euros pour calculer l'AVE
    /// Valeur par défaut: 0.005€ par impression
    /// </summary>
    public decimal CpmEur { get; set; } = 0.005m;
}
