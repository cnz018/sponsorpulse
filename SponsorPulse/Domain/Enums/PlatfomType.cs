using System.ComponentModel;

namespace SponsorPulse.Domain.Entities;

public enum PlatformType
{
    [Description("Twitch")]
    Twitch = 0,

    [Description("Twitter")]
    Twitter = 1,

    [Description("YouTube")]
    YouTube = 2,
}
