namespace SponsorPulse.Application.Services;

using Microsoft.Extensions.Options;
using SponsorPulse.Application.Common.Configuration;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Infrastructure.Repositories;

/// <summary>
/// Représente les statistiques agrégées pour le dashboard.
/// </summary>
public record DashboardStats
{
    public int PeakViewers { get; init; }
    public long TotalEngagement { get; init; }
    public long TotalImpressions { get; init; }
    public int TotalEvents { get; init; }
    public TimeSpan TotalStreamDuration { get; init; }
    public int AverageViewers { get; init; }
}

public interface IDemoDataService
{
    Task<List<Event>> GetDemoEventsAsync();
    Task<Event?> GetDemoEventBySlugAsync(string slug);
    Task<Event?> GetDemoEventByIdAsync(Guid eventId);
    Task<DemoSettings?> GetDemoSettingsAsync();
    Task SaveDemoSettingsAsync(DemoSettings settings);
    Task<DashboardStats> GetDashboardStatsAsync();
    bool IsInDemoMode { get; }
}

public class DemoDataService(IOptions<DemoModeSettings> options) : IDemoDataService
{
    private readonly DemoModeSettings _settings = options.Value;
    private static DemoSettings? _cachedSettings;

    public bool IsInDemoMode => _settings.IsDemo;

    public async Task<List<Event>> GetDemoEventsAsync()
    {
        if (_settings.IsDemo)
        {
            return await Task.FromResult(StaticDemoRepository.GetDemoEvents());
        }

        // En mode normal, ce serait les données de la base de données
        // Pour l'instant, retourner une liste vide
        return await Task.FromResult(new List<Event>());
    }

    public async Task<Event?> GetDemoEventBySlugAsync(string slug)
    {
        if (_settings.IsDemo)
        {
            var events = StaticDemoRepository.GetDemoEvents();
            var @event = events.FirstOrDefault(e => e.Slug == slug);

            return await Task.FromResult(@event);
        }

        // En mode normal, ce serait une requête DB
        return await Task.FromResult((Event?)null);
    }

    public async Task<Event?> GetDemoEventByIdAsync(Guid eventId)
    {
        if (_settings.IsDemo)
        {
            var events = StaticDemoRepository.GetDemoEvents();
            var @event = events.FirstOrDefault(e => e.Id == eventId);
            return await Task.FromResult(@event);
        }

        // En mode normal, ce serait une requête DB
        return await Task.FromResult((Event?)null);
    }

    public Task<DemoSettings?> GetDemoSettingsAsync()
    {
        // Retourner les paramètres en cache ou les paramètres par défaut
        if (_cachedSettings == null)
        {
            _cachedSettings = new DemoSettings();
        }

        return Task.FromResult<DemoSettings?>(_cachedSettings);
    }

    public Task SaveDemoSettingsAsync(DemoSettings settings)
    {
        // Sauvegarder les paramètres en cache
        settings.LastModified = DateTime.UtcNow;
        _cachedSettings = settings;
        return Task.CompletedTask;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var events = await GetDemoEventsAsync();

        if (events == null || events.Count == 0)
        {
            return new DashboardStats
            {
                PeakViewers = 0,
                TotalEngagement = 0,
                TotalImpressions = 0,
                TotalEvents = 0,
                TotalStreamDuration = TimeSpan.Zero,
                AverageViewers = 0
            };
        }

        var peakViewers = events.Max(e => e.PeakViewers ?? 0);
        var totalEngagement = events.Sum(e => e.TwitterAnalytics?.TotalEngagement ?? 0);
        var totalImpressions = events.Sum(e => e.TwitterAnalytics?.EstimatedImpressions ?? 0);
        var totalStreamDurationTicks = events.Sum(e => (e.StreamDuration ?? TimeSpan.Zero).Ticks);
        var totalViewerCount = events.Sum(e => e.ViewerCount ?? 0);

        return new DashboardStats
        {
            PeakViewers = peakViewers,
            TotalEngagement = totalEngagement,
            TotalImpressions = totalImpressions,
            TotalEvents = events.Count,
            TotalStreamDuration = TimeSpan.FromTicks(totalStreamDurationTicks),
            AverageViewers = totalViewerCount / events.Count
        };
    }
}
