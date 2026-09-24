using System.Threading.RateLimiting;
using Duende.IdentityServer;
using Duende.IdentityServer.AspNetIdentity;
using Duende.IdentityServer.Models;
using LumexUI.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Infrastructure.Api.Extensions;
using SponsorPulse.Infrastructure.DependencyInjection;
using SponsorPulse.Infrastructure.Persistence;
using SponsorPulse.Presentation;
using SponsorPulse.Presentation.Services;

// Initialiser SQLitePCL
SQLitePCL.Batteries_V2.Init();

var builder = WebApplication.CreateBuilder(args);

// Configure Logging
builder.Logging.AddDebug();

// Add Blazor Web Services
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddLumexServices();

// Infrastructure Services
builder.Services.AddInfrastructure(builder.Configuration);

// Register HttpClient
builder.Services.AddHttpClient();

// fix le cookie pendant le développement local (localhost) pour l'authentification
builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "persistedKeys"))
    )
    .SetApplicationName("SponsorPulseApp");

// Register Presigned URL API Service
builder.Services.AddScoped<PresignedUrlApiService>();

// Database Context
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=SponsorPulse.db;Cache=Shared;Foreign Keys=False";
var analyticsConnectionString =
    builder.Configuration.GetConnectionString("AnalyticsConnection")
    ?? "Data Source=sponsorpulse_analytics.db";

builder.Services.AddDbContextFactory<SponsorPulseDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

builder.Services.AddDbContextFactory<SponsorPulseAnalyticsDbContext>(options =>
{
    options.UseSqlite(analyticsConnectionString);
});

// Identity + auth
builder
    .Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
    })
    .AddEntityFrameworkStores<SponsorPulseDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
});
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Permet de propager l'état d'authentification du serveur vers le client WebAssembly
builder.Services.AddCascadingAuthenticationState();

// Rate Limiting for auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "AuthPolicy",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(5),
                }
            )
    );
});

// Minimal IdentityServer configuration (in-memory) for development
builder
    .Services.AddIdentityServer(options =>
    {
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;
    })
    .AddAspNetIdentity<ApplicationUser>()
    .AddInMemoryIdentityResources([new IdentityResources.OpenId(), new IdentityResources.Profile()])
    .AddInMemoryApiScopes(new ApiScope[] { new ApiScope("sponsor_api", "SponsorPulse API") })
    .AddInMemoryClients(
        new Client[]
        {
            new Client
            {
                ClientId = "sponsorpulse_api_client",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Duende.IdentityServer.Models.Secret("dev_secret".Sha256()) },
                AllowedScopes = { "sponsor_api" },
            },
        }
    )
    .AddDeveloperSigningCredential();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<
        IDbContextFactory<SponsorPulseDbContext>
    >();
    using var context = await dbFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();

    var analyticsFactory = scope.ServiceProvider.GetRequiredService<
        IDbContextFactory<SponsorPulseAnalyticsDbContext>
    >();
    await using var analyticsContext = await analyticsFactory.CreateDbContextAsync();
    await analyticsContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseRateLimiter();
app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRegisterEndpoints();
app.MapLoginEndpoints();

// Map Media API endpoints (presigned URLs, etc.)
app.MapMediaPresignedUrlEndpoints();

// Map Twitch OAuth endpoints for user authorization flow
app.MapTwitchAuthEndpoints();

// Map Twitch analytics endpoints
app.MapTwitchAnalyticsEndpoints();
app.MapTwitchWebhookEndpoints();

// Map Waitlist API endpoint
app.MapWaitlistEndpoint();

// Storytelling endpoint (Azure Function style)
app.MapStorytellingFunctionEndpoints();

// Map Blazor Components
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.UseCors();
app.Run();
