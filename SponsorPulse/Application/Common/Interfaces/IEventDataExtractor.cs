using SponsorPulse.Application.Common.Models;

namespace SponsorPulse.Application.Common.Interfaces;

public interface IEventDataExtractor
{
    Task<EventRawData?> ExtractAsync(string slug, CancellationToken cancellationToken = default);
}
