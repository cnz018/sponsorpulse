using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Primitives;
using SponsorPulse.Infrastructure.Repositories;

namespace SponsorPulse.Infrastructure.Services;

/// <summary>
/// Service pour accéder aux rapports Twitter statiques.
/// Utilisé en développement ou quand l'API externe n'est pas disponible.
/// </summary>
public interface IStaticReportService
{
    Task<Result<TwitterAnalytics>> GetReportAsync(string reportKey);
    Task<Dictionary<string, TwitterAnalytics>> GetAllReportsAsync();
    Task<TwitterAnalytics> GetCustomEventReportAsync(string eventName, string topInfluencerHandle);
}

/// <summary>
/// Implémentation du service de rapports statiques.
/// </summary>
public class StaticReportService : IStaticReportService
{
    public Task<Result<TwitterAnalytics>> GetReportAsync(string reportKey)
    {
        try
        {
            var reports = StaticReportRepository.GetAllReports();

            if (reports.TryGetValue(reportKey, out var report))
            {
                return Task.FromResult(Result<TwitterAnalytics>.Success(report));
            }

            return Task.FromResult(
                Result<TwitterAnalytics>.Failure(
                    $"Rapport '{reportKey}' non trouvé. Rapports disponibles: {string.Join(", ", reports.Keys)}"
                )
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Result<TwitterAnalytics>.Failure(
                    $"Erreur lors de la récupération du rapport: {ex.Message}"
                )
            );
        }
    }

    public Task<Dictionary<string, TwitterAnalytics>> GetAllReportsAsync()
    {
        return Task.FromResult(StaticReportRepository.GetAllReports());
    }

    public Task<TwitterAnalytics> GetCustomEventReportAsync(
        string eventName,
        string topInfluencerHandle
    )
    {
        return Task.FromResult(
            StaticReportRepository.GetCustomEventReport(eventName, topInfluencerHandle)
        );
    }
}
