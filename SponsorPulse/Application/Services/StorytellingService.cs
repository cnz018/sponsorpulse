using System.Net;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Application.Services;

public sealed class StorytellingService(
    IEventDataExtractor eventDataExtractor,
    ILlmApiClient llmApiClient
) : IStorytellingService
{
    private const string EventNotFoundMessage = "L'événement demandé est introuvable.";

    public async Task<ApiCallResponse<StorytellingReponse>> GenerateStorytellingAsync(
        string slug,
        CancellationToken cancellationToken = default
    )
    {
        var eventData = await eventDataExtractor.ExtractAsync(slug, cancellationToken);

        return eventData is null
            ? ApiCallResponse<StorytellingReponse>.Failed(
                HttpStatusCode.NotFound,
                EventNotFoundMessage
            )
            : await llmApiClient.GenerateAsync(eventData, cancellationToken);
    }
}
