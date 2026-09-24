using SponsorPulse.Application.Common.Models;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Application.Common.Interfaces;

public interface ILlmApiClient
{
    Task<ApiCallResponse<StorytellingReponse>> GenerateAsync(
        EventRawData data,
        CancellationToken cancellationToken = default
    );
}
