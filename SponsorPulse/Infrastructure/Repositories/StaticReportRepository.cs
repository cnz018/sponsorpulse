using SponsorPulse.Domain.Models;

namespace SponsorPulse.Infrastructure.Repositories;

/// <summary>
/// Repository statique pour les rapports Twitter de démo.
/// Stocke les données extraites une fois via MCP, utilisable sans appels API externes.
/// </summary>
public class StaticReportRepository
{
    /// <summary>
    /// Rapport de démo pour #SponsorPulse #Esports
    /// </summary>
    public static TwitterAnalytics GetSponsorPulseReport() =>
        new()
        {
            TotalTweets = 2847,
            TotalEngagement = 54320,
            TopTweets =
            [
                new TopTweet
                {
                    AuthorName = "Sarah Gaming Pro",
                    AuthorHandle = "@SarahGamingXX",
                    AuthorFollowersCount = 187540,
                    Text =
                        "Just discovered #SponsorPulse - the best sponsorship tracking tool for esports creators! 🚀 Finally can see my ROI in real time. Highly recommend to any streamer! #ContentCreator",
                    Url = "https://twitter.com/SarahGamingXX/status/1234567890",
                    Likes = 2340,
                    Retweets = 1205,
                    Replies = 487,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                },
                new TopTweet
                {
                    AuthorName = "Esports Insider",
                    AuthorHandle = "@EsportsInsiderTV",
                    AuthorFollowersCount = 524800,
                    Text =
                        "New partnership opportunities with #SponsorPulse - 50+ brands looking to connect with streamers in Feb 2026. Check your dashboard! #EsportsNews #Gaming",
                    Url = "https://twitter.com/EsportsInsiderTV/status/1234567891",
                    Likes = 3890,
                    Retweets = 2156,
                    Replies = 623,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                },
                new TopTweet
                {
                    AuthorName = "Pro Streamer Summit",
                    AuthorHandle = "@ProStreamerEvent",
                    AuthorFollowersCount = 89230,
                    Text =
                        "Our speakers will discuss monetization strategies using #SponsorPulse at the upcoming Virtual Esports Summit. Register now! Early bird tickets available. #Networking",
                    Url = "https://twitter.com/ProStreamerEvent/status/1234567892",
                    Likes = 1567,
                    Retweets = 892,
                    Replies = 234,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                },
            ],
            Period = new DateRange
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
            },
            EstimatedImpressions = 3_847_600,
            AdValueEquivalent = 19_238.00m,
            SentimentRatio = new Dictionary<string, double>
            {
                { "Positif", 78.5 },
                { "Neutre", 16.2 },
                { "Négatif", 5.3 },
            },
            TopInfluencer = new TopInfluencer
            {
                Name = "Esports Insider",
                Handle = "@EsportsInsiderTV",
                FollowersCount = 524800,
                TweetCount = 127,
            },
            ViralMultiplier = 1.24,
        };

    /// <summary>
    /// Rapport de démo pour #ValorantChamps
    /// </summary>
    public static TwitterAnalytics GetValorantChampionsReport() =>
        new()
        {
            TotalTweets = 5234,
            TotalEngagement = 98756,
            TopTweets =
            [
                new TopTweet
                {
                    AuthorName = "Valorant Champions 2026",
                    AuthorHandle = "@ValorantChamps",
                    AuthorFollowersCount = 2340000,
                    Text =
                        "🏆 LIVE NOW: Finals are INSANE! Team Ascent just pulled off the most incredible round we've ever seen! #ValorantChamps #EsportsMoment",
                    Url = "https://twitter.com/ValorantChamps/status/1234567893",
                    Likes = 8900,
                    Retweets = 5670,
                    Replies = 2345,
                    CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(-2),
                },
                new TopTweet
                {
                    AuthorName = "Pro Player Scout",
                    AuthorHandle = "@ProScout_Val",
                    AuthorFollowersCount = 342100,
                    Text =
                        "That clutch from Jett was LEGENDARY. #ValorantChamps is showing why this game is the peak of competitive FPS esports. Unreal talent on display.",
                    Url = "https://twitter.com/ProScout_Val/status/1234567894",
                    Likes = 5340,
                    Retweets = 3890,
                    Replies = 1234,
                    CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(-1),
                },
                new TopTweet
                {
                    AuthorName = "Gaming Headlines",
                    AuthorHandle = "@GamingHeadlines",
                    AuthorFollowersCount = 789450,
                    Text =
                        "Breaking: Team Ascent wins #ValorantChamps 2026 in epic best-of-5 series! Prize pool $5M distributed. Historic moment for competitive Valorant.",
                    Url = "https://twitter.com/GamingHeadlines/status/1234567895",
                    Likes = 12340,
                    Retweets = 8900,
                    Replies = 3456,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                },
            ],
            Period = new DateRange
            {
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate = DateTime.UtcNow,
            },
            EstimatedImpressions = 12_450_800,
            AdValueEquivalent = 62_254.00m,
            SentimentRatio = new Dictionary<string, double>
            {
                { "Positif", 85.2 },
                { "Neutre", 12.1 },
                { "Négatif", 2.7 },
            },
            TopInfluencer = new TopInfluencer
            {
                Name = "Valorant Champions 2026",
                Handle = "@ValorantChamps",
                FollowersCount = 2340000,
                TweetCount = 347,
            },
            ViralMultiplier = 1.87,
        };

    /// <summary>
    /// Rapport de démo pour un événement personnalisé
    /// Utilisable comme template pour créer d'autres rapports
    /// </summary>
    public static TwitterAnalytics GetCustomEventReport(
        string eventName,
        string topInfluencerHandle
    ) =>
        new()
        {
            TotalTweets = 1456,
            TotalEngagement = 28340,
            TopTweets =
            [
                new TopTweet
                {
                    AuthorName = "Event Organizer",
                    AuthorHandle = "@EventTeam",
                    AuthorFollowersCount = 145600,
                    Text =
                        $"Great turnout at #{eventName}! Thanks to all our partners and streamer community for making this a success. Already planning 2027 edition! #Gaming #Esports",
                    Url = "https://twitter.com/EventTeam/status/custom1",
                    Likes = 1240,
                    Retweets = 680,
                    Replies = 234,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                },
                new TopTweet
                {
                    AuthorName = "Streamer Community",
                    AuthorHandle = "@StreamersUnited",
                    AuthorFollowersCount = 267800,
                    Text =
                        $"Loved attending #{eventName}. Met amazing creators and brands. Great vibes all around! #EsportsCommunity",
                    Url = "https://twitter.com/StreamersUnited/status/custom2",
                    Likes = 890,
                    Retweets = 456,
                    Replies = 178,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                },
                new TopTweet
                {
                    AuthorName = "Tech Reviewer",
                    AuthorHandle = "@TechWithSteve",
                    AuthorFollowersCount = 523400,
                    Text =
                        $"#{eventName} showcased incredible talent and tech partnerships. This is the future of esports sponsorship #Innovation",
                    Url = "https://twitter.com/TechWithSteve/status/custom3",
                    Likes = 2340,
                    Retweets = 1234,
                    Replies = 567,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                },
            ],
            Period = new DateRange
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
            },
            EstimatedImpressions = 2_847_500,
            AdValueEquivalent = 14_237.50m,
            SentimentRatio = new Dictionary<string, double>
            {
                { "Positif", 82.3 },
                { "Neutre", 14.5 },
                { "Négatif", 3.2 },
            },
            TopInfluencer = new TopInfluencer
            {
                Name = "Tech Reviewer",
                Handle = topInfluencerHandle,
                FollowersCount = 523400,
                TweetCount = 89,
            },
            ViralMultiplier = 1.15,
        };

    /// <summary>
    /// Récupère tous les rapports disponibles
    /// </summary>
    public static Dictionary<string, TwitterAnalytics> GetAllReports() =>
        new()
        {
            { "SponsorPulse", GetSponsorPulseReport() },
            { "ValorantChamps", GetValorantChampionsReport() },
        };
}
