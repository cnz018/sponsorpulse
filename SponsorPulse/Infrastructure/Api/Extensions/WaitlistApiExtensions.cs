using SponsorPulse.Infrastructure.Services;

namespace SponsorPulse.Infrastructure.Api.Extensions;

/// <summary>
/// Extension methods for mapping waitlist API endpoints.
/// Follows Clean Architecture by separating endpoint configuration from Program.cs.
/// </summary>
public static class WaitlistApiExtensions
{
    /// <summary>
    /// Maps the waitlist endpoint to the application.
    /// </summary>
    /// <param name="app">The web application builder</param>
    /// <returns>The web application for further configuration</returns>
    public static WebApplication MapWaitlistEndpoint(this WebApplication app)
    {
        app.MapPost(
            "/api/waitlist",
            async (
                WaitlistRequest request,
                IWaitlistService waitlistService,
                ILogger<WaitlistHandler> logger,
                CancellationToken cancellationToken
            ) =>
            {
                var handler = new WaitlistHandler();
                return await handler.HandleJoinWaitlist(
                    request,
                    waitlistService,
                    logger,
                    cancellationToken
                );
            }
        );

        app.MapGet(
            "/api/waitlist/count",
            (IWaitlistService waitlistService) =>
            {
                return Results.Ok(new { count = waitlistService.GetWaitlistCount() });
            }
        );

        return app;
    }
}

/// <summary>
/// Request model for waitlist signup.
/// </summary>
public record WaitlistRequest(string Email);

/// <summary>
/// Request handler for waitlist operations.
/// Handles the business logic of adding users to the waitlist and sending confirmation emails.
/// </summary>
public class WaitlistHandler
{
    /// <summary>
    /// Handles POST requests to add users to the waitlist.
    /// Validates email, sends confirmation email via CloudMailin, and returns success/error.
    /// </summary>
    /// <param name="request">The waitlist request containing email</param>
    /// <param name="waitlistService">The waitlist service for email and counter</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Success response or error response</returns>
    public async Task<IResult> HandleJoinWaitlist(
        WaitlistRequest request,
        IWaitlistService waitlistService,
        ILogger<WaitlistHandler> logger,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Validate request
            if (request is null || string.IsNullOrWhiteSpace(request.Email))
            {
                logger.LogWarning("Invalid request: missing email");
                return Results.BadRequest(new { error = "Email is required." });
            }

            // Basic email validation
            if (!request.Email.Contains("@") || !request.Email.Contains("."))
            {
                logger.LogWarning("Invalid email format: {Email}", request.Email);
                return Results.BadRequest(new { error = "Invalid email format." });
            }

            logger.LogInformation("Adding email to waitlist: {Email}", request.Email);

            // Add to waitlist (sends email via CloudMailin)
            var success = await waitlistService.AddToWaitlistAsync(request.Email);

            if (!success)
            {
                logger.LogError("Failed to add email to waitlist: {Email}", request.Email);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            logger.LogInformation("Successfully added email to waitlist: {Email}", request.Email);

            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding email to waitlist");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
