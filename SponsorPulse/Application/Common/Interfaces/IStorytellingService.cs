using SponsorPulse.Application.Common.Models;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Application.Common.Interfaces;

/// <summary>
/// Interface pour le service de storytelling qui génère des réponses basées sur les données Twitter et Twitch.
/// </summary>
public interface IStorytellingService
{
    /// <summary>
    /// Génère une réponse de storytelling en analysant les données Twitter et Twitch.
    /// </summary>
    /// <param name="request">Les données brutes Twitter et Twitch à analyser.</param>
    /// <returns>La réponse structurée contenant les slides de storytelling.</returns>
    Task<ApiCallResponse<StorytellingReponse>> GenerateStorytellingAsync(
        string slug,
        CancellationToken cancellationToken = default
    );
}
