using Microsoft.AspNetCore.Mvc;
using SponsorPulse.Infrastructure.Services;

namespace SponsorPulse.Presentation.Controllers;

/// <summary>
/// API Controller pour accéder aux rapports Twitter statiques.
/// Utilisé en développement ou comme fallback quand l'API Xpoz n'est pas disponible.
/// </summary>
[ApiController]
[Route("api/reports/twitter")]
public class StaticReportsController(IStaticReportService reportService) : ControllerBase
{
    /// <summary>
    /// Récupère un rapport spécifique par clé
    /// </summary>
    /// <param name="reportKey">Clé du rapport (ex: SponsorPulse, ValorantChamps)</param>
    [HttpGet("{reportKey}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetReport(string reportKey)
    {
        var result = await reportService.GetReportAsync(reportKey);

        if (!result.IsSuccess)
        {
            return NotFound(new { message = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Récupère tous les rapports disponibles
    /// </summary>
    [HttpGet("all")]
    [Produces("application/json")]
    public async Task<IActionResult> GetAllReports()
    {
        var reports = await reportService.GetAllReportsAsync();
        return Ok(reports);
    }

    /// <summary>
    /// Génère un rapport personnalisé pour un événement
    /// </summary>
    /// <param name="eventName">Nom de l'événement</param>
    /// <param name="topInfluencerHandle">Handle Twitter du top influenceur</param>
    [HttpGet("custom")]
    [Produces("application/json")]
    public async Task<IActionResult> GetCustomEventReport(
        [FromQuery] string eventName,
        [FromQuery] string topInfluencerHandle = "@TopInfluencer"
    )
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return BadRequest(new { message = "eventName query parameter is required" });
        }

        var report = await reportService.GetCustomEventReportAsync(eventName, topInfluencerHandle);

        return Ok(report);
    }
}
