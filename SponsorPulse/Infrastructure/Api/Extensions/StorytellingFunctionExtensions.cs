using SponsorPulse.Application.Common.Interfaces;

namespace SponsorPulse.Infrastructure.Api.Extensions;

public static class StorytellingFunctionExtensions
{
    public static WebApplication MapStorytellingFunctionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/storytelling/generate/{slug}", GenerateStorytelling)
            .WithName("GenerateStorytelling");

        return app;
    }

    private static async Task<IResult> GenerateStorytelling(
        string slug,
        IStorytellingService storytellingService,
        CancellationToken cancellationToken
    )
    {
        var response = await storytellingService.GenerateStorytellingAsync(slug, cancellationToken);

        return response.Success
            ? Results.Ok(response)
            : Results.StatusCode((int)response.StatusCode);
    }
}
