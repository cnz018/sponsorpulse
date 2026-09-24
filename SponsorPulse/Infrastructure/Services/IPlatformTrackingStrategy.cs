using SponsorPulse.Doimain.Enums;

namespace SponsorPulse.Infrastructure.Services;

public interface IPlatformTrackingStrategy
{
    StreamPlatform Platform { get; }
    Task<object> CaptureAsync(string target, Guid? userId = null);
}
