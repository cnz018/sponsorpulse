using SponsorPulse.Application.Common.Interfaces;

namespace SponsorPulse.Infrastructure.Api.Extensions;

public static class TwitchAnalyticsExtensions
{
    public static WebApplication MapTwitchAnalyticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/twitch/analytics/extension", GetExtensionCsvUrl)
            .WithName("GetExtensionAnalyticsCsv");

        app.MapGet("/api/twitch/analytics/game", GetGameCsvUrl).WithName("GetGameAnalyticsCsv");

        return app;
    }

    private static async Task<IResult> GetExtensionCsvUrl(
        string extensionId,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        string? ownerTwitchUserId,
        bool parse,
        ITwitchService twitchService
    )
    {
        if (string.IsNullOrEmpty(extensionId))
            return Results.BadRequest(new { error = "extensionId is required" });

        var start = startedAt ?? DateTimeOffset.UtcNow.AddDays(-7);
        var end = endedAt ?? DateTimeOffset.UtcNow;

        if (parse)
        {
            var parsed = await twitchService.GetExtensionAnalyticsAsync(
                extensionId,
                start,
                end,
                null,
                ownerTwitchUserId
            );
            if (!parsed.IsSuccess)
                return Results.BadRequest(new { error = parsed.ErrorMessage });

            return Results.Ok(parsed.Value);
        }

        var result = await twitchService.GetExtensionAnalyticsCsvUrlAsync(
            extensionId,
            start,
            end,
            null,
            ownerTwitchUserId
        );
        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.ErrorMessage });

        return Results.Ok(new { url = result.Value });
    }

    private static async Task<IResult> GetGameCsvUrl(
        string gameId,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        string? ownerTwitchUserId,
        bool parse,
        ITwitchService twitchService
    )
    {
        if (string.IsNullOrEmpty(gameId))
            return Results.BadRequest(new { error = "gameId is required" });

        var start = startedAt ?? DateTimeOffset.UtcNow.AddDays(-7);
        var end = endedAt ?? DateTimeOffset.UtcNow;

        if (parse)
        {
            var parsed = await twitchService.GetGameAnalyticsAsync(
                gameId,
                start,
                end,
                null,
                ownerTwitchUserId
            );
            if (!parsed.IsSuccess)
                return Results.BadRequest(new { error = parsed.ErrorMessage });

            return Results.Ok(parsed.Value);
        }

        var result = await twitchService.GetGameAnalyticsCsvUrlAsync(
            gameId,
            start,
            end,
            null,
            ownerTwitchUserId
        );
        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.ErrorMessage });

        return Results.Ok(new { url = result.Value });
    }
}
