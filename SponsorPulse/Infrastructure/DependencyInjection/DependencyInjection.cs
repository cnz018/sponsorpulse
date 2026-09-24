using SponsorPulse.Application.Common.Config; // Added to resolve LlmSettings
using SponsorPulse.Application.Common.Configuration;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Services;
using SponsorPulse.Infrastructure.CloudflareR2;
using SponsorPulse.Infrastructure.Persistence;
using SponsorPulse.Infrastructure.Repositories;
using SponsorPulse.Infrastructure.Services;
using SponsorPulse.Infrastructure.Xpoz.Strategies;

namespace SponsorPulse.Infrastructure.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddScoped<ITwitchService, TwitchInfrastructureService>();
        services.AddScoped<ITwitchAuthStateService, TwitchAuthStateManager>();
        services.AddScoped<ITwitterService, TwitterInfrastructureService>();
        services.AddScoped<IXpozPlatformStrategy, XpozTwitterStrategy>();
        services.AddScoped<ISocialMediaAnalyticsService, SocialMediaAnalyticsService>();
        services.AddScoped<ISocialPostRepository, SocialPostRepository>();
        services.AddScoped<IStaticReportService, StaticReportService>();
        services.AddScoped<ILinkedAccountManager, LinkedAccountManager>();

        // Demo Mode Configuration
        services.AddOptions<DemoModeSettings>().Bind(configuration.GetSection("DemoMode"));

        // Waitlist Configuration (Infomaniak SMTP)
        services.AddOptions<WaitlistSettings>().Bind(configuration.GetSection("WaitlistSettings"));

        // Value Calculator Configuration
        services
            .AddOptions<ValueCalculatorSettings>()
            .Bind(configuration.GetSection("ValueCalculator"));

        services.AddOptions<ReportSettings>().Bind(configuration.GetSection("ReportSettings"));

        // Application Services
        services.AddScoped<IDemoDataService, DemoDataService>();
        services.AddScoped<IAnalysisSimulatorService, AnalysisSimulatorService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<IValueCalculatorService, ValueCalculatorService>();

        // Waitlist Service (Singleton pour le compteur)
        services.AddSingleton<IWaitlistService, WaitlistService>();

        services.Configure<XpozSettings>(configuration.GetSection("XpozSettings"));
        services.AddSingleton(TimeProvider.System);
        services.Configure<R2Settings>(configuration.GetSection("CloudflareR2"));
        services.AddSingleton<IR2ClientFactory, R2ClientFactory>();
        services.AddScoped<IMediaStorageService, MediaStorageService>();

        // LLM Configuration
        services.Configure<LlmSettings>(configuration.GetSection("LlmSettings"));
        services.AddSingleton(serviceProvider =>
            serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmSettings>>()
                .Value
        );

        // Storytelling Service
        services.AddScoped<IEventDataExtractor, EventDataExtractor>();
        services.AddHttpClient<ILlmApiClient, LlmApiClient>(
            (serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<LlmSettings>();
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            }
        );
        services.AddScoped<IStorytellingService, StorytellingService>();

        return services;
    }
}
