using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SponsorPulse.Domain.Entities;

namespace SponsorPulse.Infrastructure.Api.Extensions;

public static class AuthenticationExtensions
{
    public static WebApplication MapLoginEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/auth/login",
                async (
                    HttpContext httpContext,
                    ILogger<Program> logger,
                    SignInManager<ApplicationUser> signInManager,
                    UserManager<ApplicationUser> userManager
                ) =>
                {
                    try
                    {
                        var form = await httpContext.Request.ReadFormAsync();
                        var email = form["email"];
                        var password = form["password"];
                        var rememberMe = form["rememberMe"] == "true";

                        logger.LogInformation(
                            "Tentative de connexion pour l'email: {Email}",
                            email
                        );

                        var user = await userManager.FindByEmailAsync(email!);

                        if (user is null)
                        {
                            logger.LogWarning("Utilisateur non trouvé: {Email}", email);
                            return Results.Redirect("/login?error=invalid_credentials");
                        }

                        var result = await signInManager.PasswordSignInAsync(
                            user,
                            password!,
                            isPersistent: true,
                            lockoutOnFailure: false
                        );

                        if (!result.Succeeded)
                        {
                            logger.LogWarning(
                                "Connexion échouée pour: {Email}. Raison: {Reason}",
                                email,
                                result.IsLockedOut ? "Locked out" : "Invalid password"
                            );
                            return Results.Redirect("/login?error=invalid_credentials");
                        }

                        if (result.IsLockedOut)
                        {
                            logger.LogWarning("Compte verrouillé: {Email}", email);
                            return Results.Redirect("/login?error=locked_out");
                        }

                        logger.LogInformation("Connexion réussie pour: {Email}", email);
                        return Results.Redirect("/dashboard");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Erreur lors de la tentative de connexion");
                        return Results.Redirect("/login?error=server_error");
                    }
                }
            )
            .RequireRateLimiting("AuthPolicy")
            .DisableAntiforgery();

        return app;
    }

    public static WebApplication MapRegisterEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/auth/register",
                async (
                    HttpContext context,
                    ILogger<Program> logger,
                    UserManager<ApplicationUser> userManager,
                    SignInManager<ApplicationUser> signInManager
                ) =>
                {
                    try
                    {
                        var form = await context.Request.ReadFormAsync();
                        var firstName = form["firstName"];
                        var organizationName = form["organizationName"];
                        var email = form["email"];
                        var password = form["password"];
                        var confirmPassword = form["confirmPassword"];

                        logger.LogInformation(
                            "Tentative d'inscription pour l'email: {Email}, Nom: {FirstName}, Org: {OrgName}",
                            email,
                            firstName,
                            organizationName
                        );

                        if (password != confirmPassword)
                        {
                            logger.LogWarning(
                                "Les mots de passe ne correspondent pas pour: {Email}",
                                email
                            );
                            return Results.Redirect("/login?error=password_mismatch");
                        }

                        var user = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            EmailConfirmed = true,
                            FirstName = firstName.ToString(),
                            OrganizationName = organizationName.ToString(),
                        };

                        logger.LogDebug(
                            "Création d'utilisateur avec Email: {Email}, UserName: {UserName}",
                            email,
                            user.UserName
                        );

                        var result = await userManager.CreateAsync(user, password!);

                        if (!result.Succeeded)
                        {
                            var errors = string.Join(",", result.Errors.Select(e => e.Code));
                            logger.LogWarning(
                                "Création d'utilisateur échouée pour {Email}. Erreurs: {Errors}",
                                email,
                                errors
                            );
                            return Results.Redirect($"/login?error={errors}");
                        }

                        logger.LogInformation(
                            "Utilisateur créé avec succès. ID: {UserId}, Email: {Email}",
                            user.Id,
                            user.Email
                        );

                        // Récupérer l'utilisateur pour s'assurer que l'ID est bien défini
                        var createdUser = await userManager.FindByEmailAsync(email!);

                        if (createdUser is null)
                        {
                            logger.LogError(
                                "Impossible de récupérer l'utilisateur juste créé: {Email}",
                                email
                            );
                            return Results.Redirect("/login?error=user_creation_failed");
                        }

                        logger.LogDebug(
                            "Utilisateur récupéré. ID: {UserId}, Email: {Email}, UserName: {UserName}",
                            createdUser.Id,
                            createdUser.Email,
                            createdUser.UserName
                        );

                        var claims = new List<Claim>
                        {
                            new("sub", createdUser.Id.ToString()),
                            new(ClaimTypes.Name, createdUser.UserName),
                            new(ClaimTypes.Email, createdUser.Email),
                        };

                        await signInManager.SignInWithClaimsAsync(createdUser, false, claims);
                        logger.LogInformation(
                            "Inscription réussie et connexion automatique pour: {Email}",
                            createdUser.Email
                        );

                        return Results.Redirect("/dashboard");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Erreur lors de la tentative d'inscription: {Message}",
                            ex.Message
                        );
                        return Results.Redirect("/login?error=server_error");
                    }
                }
            )
            .RequireRateLimiting("AuthPolicy")
            .DisableAntiforgery();

        return app;
    }
}
