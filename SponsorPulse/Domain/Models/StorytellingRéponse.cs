namespace SponsorPulse.Domain.Models;

using SponsorPulse.Domain.Enums;

/// <summary>
/// Réponse du service de storytelling contenant une liste de slides pour le PDF
/// </summary>
public record StorytellingReponse
{
    /// <summary>
    /// Liste des slides générées par le storytelling
    /// </summary>
    public List<Slide> Slides { get; init; } = new();

    /// <summary>
    /// Slide individuelle du storytelling
    /// </summary>
    public record Slide
    {
        /// <summary>
        /// Type fonctionnel de la slide utilisé pour son rendu dans le rapport.
        /// </summary>
        public StorytellingSlideType Type { get; init; } = StorytellingSlideType.Unknown;

        /// <summary>
        /// Titre de la slide
        /// </summary>
        public string Titre { get; init; } = string.Empty;

        /// <summary>
        /// Contenu principal de la slide (peut être du texte enrichi ou du markdown)
        /// </summary>
        public string Contenu { get; init; } = string.Empty;

        /// <summary>
        /// URL de l'image associée à la slide (optionnelle)
        /// </summary>
        public string? ImageUrl { get; init; }

        /// <summary>
        /// Indicateur si la slide contient une visualisation de données (graphique, métrique)
        /// </summary>
        public bool EstVisualisation { get; init; } = false;

        /// <summary>
        /// Données brutes pour la visualisation (si EstVisualisation est vrai)
        /// </summary>
        public object? DonneesVisualisation { get; init; }
    }
}
