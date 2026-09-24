namespace SponsorPulse.Infrastructure.Repositories;

using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Models;

/// <summary>
/// Static repository providing demo data for showcasing the application.
/// All data is deterministic and reproducible based on event ID.
/// </summary>
public static class StaticDemoRepository
{
    private static readonly Random Seeded = new(42); // Fixed seed for reproducibility

    public static List<Event> GetDemoEvents()
    {
        return new()
        {
            CreateDemoEvent(
                id: Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                name: "LEC Spring Finals 2026",
                description: "League of Legends Championship Spring Finals",
                date: new DateTime(2026, 4, 15, 18, 0, 0),
                platform: "Twitch",
                broadcasterId: "lec",
                slug: "lec2026a"
            ),
            CreateDemoEvent(
                id: Guid.Parse("550e8400-e29b-41d4-a716-446655440002"),
                name: "Valorant Champions Tour",
                description: "Valorant Pro esports competition",
                date: new DateTime(2026, 3, 20, 16, 30, 0),
                platform: "Twitch",
                broadcasterId: "valorantesports",
                slug: "valo2026b"
            ),
            CreateDemoEvent(
                id: Guid.Parse("550e8400-e29b-41d4-a716-446655440003"),
                name: "CS2 Major 2026",
                description: "Counter-Strike 2 Major Championship",
                date: new DateTime(2026, 5, 10, 14, 0, 0),
                platform: "Twitch",
                broadcasterId: "esl_csgo",
                slug: "cs2maj26c"
            ),
            CreateDemoEvent(
                id: Guid.Parse("550e8400-e29b-41d4-a716-446655440004"),
                name: "Dota 2 International Qualifiers",
                description: "Path to the International",
                date: new DateTime(2026, 2, 28, 19, 0, 0),
                platform: "YouTube",
                broadcasterId: "dotaesports",
                slug: "dota2i26d"
            ),
            CreateDemoEvent(
                id: Guid.Parse("550e8400-e29b-41d4-a716-446655440005"),
                name: "PUBG Global Championship",
                description: "Battle Royale World Championship",
                date: new DateTime(2026, 6, 5, 17, 0, 0),
                platform: "Twitch",
                broadcasterId: "pubgesports",
                slug: "pubg2026e"
            ),
        };
    }

    public static TwitchMetrics GetTwitchMetrics(Guid eventId)
    {
        // Deterministic random data based on eventId
        var random = new Random(eventId.GetHashCode());

        var viewerCount = random.Next(15000, 85000);
        var peakViewers = random.Next(80000, 250000);
        var streamDuration = TimeSpan.FromHours(random.Next(4, 12));
        var uniqueViewers = (int)(viewerCount * random.NextDouble() * 0.5 + viewerCount);
        var viewerHours = uniqueViewers * streamDuration.TotalHours;
        var engagementRate = random.Next(15, 45) / 100.0;
        var estimatedRoi = (decimal)(viewerHours * random.Next(5, 15));
        var cpm = (decimal)(random.Next(8, 25) + random.NextDouble() * 0.99);

        // Chat metrics
        var totalMessages = random.Next(50000, 500000);
        var uniqueChatters = random.Next(5000, 50000);
        var messagesPerMinute = totalMessages / (int)streamDuration.TotalMinutes;
        var emoteUsageRate = random.Next(40, 80) / 100.0;
        var topEmotes = new List<string> { "PogChamp", "Kappa", "LUL", "EZ", "Hype" };

        // Viewer retention
        var avgRetentionRate = random.Next(60, 90) / 100.0;
        var peakRetentionRate = random.Next(85, 98) / 100.0;
        var avgWatchTime = TimeSpan.FromMinutes(streamDuration.TotalMinutes * avgRetentionRate);
        var completionRate = random.Next(25, 55) / 100.0;

        // Sponsor exposure
        var mentionCount = random.Next(10, 50);
        var totalExposureTime = TimeSpan.FromMinutes(random.Next(30, 180));
        var logoImpressions = (int)(uniqueViewers * random.NextDouble() * 0.8);
        var brandSentimentScore = random.Next(65, 95) / 100.0;

        // Conversion metrics
        var clicks = random.Next(1000, 10000);
        var visits = random.Next(5000, 25000);
        var conversionRate = clicks / (double)visits;
        var revenuePerViewer = (decimal)(random.Next(10, 100) + random.NextDouble());
        var newFollowers = random.Next(500, 5000);

        return new TwitchMetrics
        {
            ViewerCount = viewerCount,
            PeakViewers = peakViewers,
            UniqueViewers = uniqueViewers,
            StreamDuration = streamDuration,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            GameName = GetRandomGameName(random),
            Chat = new ChatMetrics(
                TotalMessages: totalMessages,
                UniqueChatters: uniqueChatters,
                MessagesPerMinute: messagesPerMinute,
                EmoteUsageRate: emoteUsageRate,
                TopEmotes: topEmotes
            ),
            Retention = new ViewerRetention(
                AverageRetentionRate: avgRetentionRate,
                PeakRetentionRate: peakRetentionRate,
                AverageWatchTime: avgWatchTime,
                CompletionRate: completionRate
            ),
            Sponsor = new SponsorExposure(
                MentionCount: mentionCount,
                TotalExposureTime: totalExposureTime,
                LogoImpressions: logoImpressions,
                BrandSentimentScore: brandSentimentScore
            ),
            Conversion = new ConversionMetrics(
                Clicks: clicks,
                Visits: visits,
                ConversionRate: conversionRate,
                RevenuePerViewer: revenuePerViewer,
                NewFollowers: newFollowers
            ),
            AvgViewers = viewerCount * 0.85,
            ViewerHours = viewerHours,
            EngagementRate = engagementRate,
            EstimatedROI = estimatedRoi,
            CPM = cpm,
        };
    }

    public static TwitterAnalytics GetTwitterAnalytics(Guid eventId)
    {
        var random = new Random(eventId.GetHashCode() + 1);

        var totalTweets = random.Next(500, 2000);
        var totalEngagement = random.Next(50000, 250000);
        var estimatedImpressions = random.Next(500000, 2500000);
        var uniqueReach = (long)(
            estimatedImpressions * random.NextDouble() * 0.4 + estimatedImpressions * 0.3
        );
        var adValueEquivalent = random.Next(10000, 50000);
        var engagementRate = totalEngagement / (double)estimatedImpressions;
        var sentimentScore = random.Next(60, 90) / 100.0;
        var shareOfVoice = random.Next(25, 75) / 100.0;
        var estimatedRoi = (decimal)(uniqueReach * random.Next(3, 10) / 100.0);
        var cpm = (decimal)(adValueEquivalent / (estimatedImpressions / 1000.0));
        var viralMultiplier = random.Next(15, 35) / 10.0;

        // Generate top hashtags
        var topHashtags = new List<TopHashtag>
        {
            new TopHashtag(
                Tag: "LEC",
                UsageCount: random.Next(5000, 15000),
                TotalEngagements: random.Next(50000, 150000),
                ReachEstimate: random.Next(100000, 500000)
            ),
            new TopHashtag(
                Tag: "Esports",
                UsageCount: random.Next(3000, 10000),
                TotalEngagements: random.Next(30000, 100000),
                ReachEstimate: random.Next(80000, 300000)
            ),
            new TopHashtag(
                Tag: "Gaming",
                UsageCount: random.Next(2000, 8000),
                TotalEngagements: random.Next(20000, 80000),
                ReachEstimate: random.Next(50000, 200000)
            ),
        };

        // Generate geographic distribution
        var geographicReach = new List<GeographicDistribution>
        {
            new GeographicDistribution(
                "France",
                random.Next(25, 40) / 100.0,
                (int)(estimatedImpressions * 0.3)
            ),
            new GeographicDistribution(
                "Germany",
                random.Next(15, 25) / 100.0,
                (int)(estimatedImpressions * 0.2)
            ),
            new GeographicDistribution(
                "United Kingdom",
                random.Next(10, 20) / 100.0,
                (int)(estimatedImpressions * 0.15)
            ),
            new GeographicDistribution(
                "Spain",
                random.Next(8, 15) / 100.0,
                (int)(estimatedImpressions * 0.1)
            ),
            new GeographicDistribution(
                "Other",
                random.Next(10, 20) / 100.0,
                (int)(estimatedImpressions * 0.25)
            ),
        };

        // Generate top tweets with engagement rate
        var topTweets = GenerateTopTweets(random)
            .Select(t =>
                t with
                {
                    Impressions = t.Likes * random.Next(50, 150),
                    EngagementRate = (t.Likes + t.Retweets + t.Replies) / (double)(t.Likes * 100),
                }
            )
            .ToList();

        // Generate top influencers with enhanced metrics
        var topInfluencers = GenerateTopInfluencers(random);
        var topInfluencer = topInfluencers.FirstOrDefault() ?? new();
        var topInfluencerEnhanced = topInfluencer with
        {
            AvgEngagementRate = random.Next(3, 8) / 100.0,
            EstimatedReach = (long)(
                topInfluencer.FollowersCount * random.NextDouble() * 0.5
                + topInfluencer.FollowersCount * 0.3
            ),
        };

        return new TwitterAnalytics
        {
            TotalTweets = totalTweets,
            TotalEngagement = totalEngagement,
            EstimatedImpressions = estimatedImpressions,
            UniqueReach = uniqueReach,
            AdValueEquivalent = adValueEquivalent,
            EngagementRate = engagementRate,
            SentimentScore = sentimentScore,
            ShareOfVoice = shareOfVoice,
            EstimatedROI = estimatedRoi,
            CPM = cpm,
            ViralMultiplier = viralMultiplier,
            TopTweets = topTweets,
            TopHashtags = topHashtags,
            GeographicReach = geographicReach,
            TopInfluencer = topInfluencerEnhanced,
            Period = new DateRange
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow,
            },
            SentimentRatio = new Dictionary<string, double>
            {
                { "positive", 0.72 },
                { "neutral", 0.20 },
                { "negative", 0.08 },
            },
        };
    }

    private static Event CreateDemoEvent(
        Guid id,
        string name,
        string description,
        DateTime date,
        string platform,
        string broadcasterId,
        string slug
    )
    {
        var evt = new Event(id, name, description, date, platform, broadcasterId, slug)
        {
            Status = EventStatus.Completed,
        };

        // Populate Twitch metrics
        var twitchMetrics = GetTwitchMetrics(id);
        evt.ViewerCount = twitchMetrics.ViewerCount;
        evt.PeakViewers = twitchMetrics.PeakViewers;
        evt.StreamDuration = twitchMetrics.StreamDuration;
        evt.StartedAt = twitchMetrics.StartedAt.UtcDateTime;
        evt.GameName = twitchMetrics.GameName;

        // Populate Twitter analytics
        evt.TwitterAnalytics = GetTwitterAnalytics(id);

        return evt;
    }

    private static List<TopInfluencer> GenerateTopInfluencers(Random random)
    {
        return new()
        {
            new()
            {
                Name = "ProStreamers",
                Handle = "@ProStreamers",
                FollowersCount = 450000,
                TweetCount = 34,
            },
            new()
            {
                Name = "GamingNews",
                Handle = "@GamingNews",
                FollowersCount = 890000,
                TweetCount = 28,
            },
            new()
            {
                Name = "EsportsAnalyst",
                Handle = "@EsportsAnalyst",
                FollowersCount = 320000,
                TweetCount = 21,
            },
        };
    }

    private static List<TopTweet> GenerateTopTweets(Random random)
    {
        var tweets = new List<TopTweet>
        {
            new()
            {
                AuthorName = "ProPlayer99",
                AuthorHandle = "@ProPlayer99",
                AuthorFollowersCount = 145000,
                Text = "What an incredible match! The teamwork was insane. GGs to both teams!",
                Url = "https://twitter.com/ProPlayer99/status/1234567890",
                Likes = 12450,
                Retweets = 5680,
                Replies = 2341,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
            },
            new()
            {
                AuthorName = "EsportsCaster",
                AuthorHandle = "@EsportsCaster",
                AuthorFollowersCount = 234000,
                Text =
                    "That play was LEGENDARY! I've never seen anything like it in 10 years of casting.",
                Url = "https://twitter.com/EsportsCaster/status/1234567891",
                Likes = 18900,
                Retweets = 8234,
                Replies = 3456,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
            },
            new()
            {
                AuthorName = "TournamentOrg",
                AuthorHandle = "@TournamentOrg",
                AuthorFollowersCount = 567000,
                Text = "Congratulations to our champions! What a tournament this has been!",
                Url = "https://twitter.com/TournamentOrg/status/1234567892",
                Likes = 25670,
                Retweets = 12340,
                Replies = 4567,
                CreatedAt = DateTime.UtcNow,
            },
        };

        return tweets;
    }

    private static string GetRandomGameName(Random random)
    {
        var games = new[]
        {
            "League of Legends",
            "Valorant",
            "Counter-Strike 2",
            "Dota 2",
            "PUBG",
            "Street Fighter 6",
            "Overwatch 2",
        };
        return games[random.Next(games.Length)];
    }

    private static string GenerateSlug()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new System.Text.StringBuilder(7);

        for (int i = 0; i < 7; i++)
        {
            result.Append(chars[Seeded.Next(chars.Length)]);
        }

        return result.ToString();
    }
}
