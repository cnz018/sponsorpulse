namespace SponsorPulse.Application.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using SponsorPulse.Application.Services.Pdf;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Models;
using SponsorPulse.Infrastructure.Persistence;

public interface IPdfGenerationService
{
    Task<PdfGenerationResult> GeneratePdfAsync(Event @event, PdfCustomizationOptions options);
    Task<string> GetPdfPreviewHtmlAsync(Event @event, PdfCustomizationOptions options);
    Task<ReportModel> BuildReportModelAsync(Event @event, PdfCustomizationOptions options);
}

public record PdfCustomizationOptions(
    string CompanyName,
    string LogoUrl,
    string PrimaryColor,
    string SecondaryColor
);

public record PdfGenerationResult(
    bool Success,
    byte[]? PdfBytes,
    string? ErrorMessage,
    string GeneratedAt
);

public class PdfGenerationService : IPdfGenerationService
{
    private readonly IDbContextFactory<SponsorPulseAnalyticsDbContext> _analyticsDbFactory;
    private readonly HttpClient _httpClient;

    public PdfGenerationService(
        IDbContextFactory<SponsorPulseAnalyticsDbContext> analyticsDbFactory,
        HttpClient httpClient
    )
    {
        _analyticsDbFactory = analyticsDbFactory;
        _httpClient = httpClient;
    }

    public async Task<PdfGenerationResult> GeneratePdfAsync(
        Event @event,
        PdfCustomizationOptions options
    )
    {
        try
        {
            var reportModel = await BuildReportModelAsync(@event, options);
            var images = await LoadImagesAsync(reportModel);
            var pdfBytes = new QuestPdfReportDocument(reportModel, images).GeneratePdf();

            return new PdfGenerationResult(
                Success: true,
                PdfBytes: pdfBytes,
                ErrorMessage: null,
                GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }
        catch (Exception ex)
        {
            return new PdfGenerationResult(
                Success: false,
                PdfBytes: null,
                ErrorMessage: ex.Message,
                GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }
    }

    public async Task<string> GetPdfPreviewHtmlAsync(Event @event, PdfCustomizationOptions options)
    {
        var reportModel = await BuildReportModelAsync(@event, options);

        var suggestionsHtml = string.Concat(reportModel.Suggestions.Select(s => $"<li>{s}</li>"));
        var storytellingHtml = string.Concat(
            reportModel.StorytellingSlides.Select(slide =>
                $"<article><h4>{System.Net.WebUtility.HtmlEncode(slide.Titre)}</h4><p>{System.Net.WebUtility.HtmlEncode(slide.Contenu)}</p></article>"
            )
        );
        var impressionsStr =
            reportModel.TwitterAnalytics?.EstimatedImpressions.ToString("N0") ?? "N/A";
        var generatedDate = DateTime.Now.ToString("dd/MM/yyyy à HH:mm");

        var html =
            $@"
<!DOCTYPE html>
<html lang=""fr"">
<head>
    <meta charset=""utf-8"" />
    <title>Rapport d'Impact Sponsoring - {options.CompanyName}</title>
    <style>
        body {{ font-family: 'Georgia', serif; margin: 0; padding: 0; background: #f5f5f5; }}
        .report-wrapper {{ max-width: 210mm; margin: 2rem auto; background: white; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
        .report-content {{ padding: 40px; }}
        h1 {{ color: {reportModel.PrimaryColor}; font-size: 2.5rem; margin-bottom: 1rem; }}
        h2 {{ color: #666; font-size: 1.5rem; margin-bottom: 2rem; }}
        h3 {{ margin-top: 0; }}
        .overview-box {{ background: {reportModel.PrimaryColor}15; padding: 20px; border-radius: 12px; margin: 2rem 0; }}
        .recommendations-box {{ background: {reportModel.SecondaryColor}15; padding: 20px; border-radius: 12px; margin: 2rem 0; }}
        .footer {{ text-align: center; margin-top: 3rem; padding-top: 2rem; border-top: 2px solid #ddd; color: #999; }}
    </style>
</head>
<body>
    <div class=""report-wrapper"">
        <div class=""report-content"">
            <h1>Rapport d'Impact Sponsoring</h1>
            <h2>{reportModel.EventName}</h2>
            
            <div class=""overview-box"">
                <h3 style=""color: {reportModel.PrimaryColor};"">Vue d'ensemble</h3>
                <p><strong>Plateforme:</strong> {reportModel.EventPlatform}</p>
                <p><strong>Date:</strong> {reportModel.EventDate:dd MMMM yyyy}</p>
                <p><strong>Impressions:</strong> {impressionsStr}</p>
            </div>
            
            <div style=""margin: 2rem 0;"">
                <h3 style=""color: {reportModel.PrimaryColor};"">Storytelling</h3>
                <div style=""line-height: 1.8;"">{storytellingHtml}</div>
            </div>
            
            <div class=""recommendations-box"">
                <h3 style=""color: {reportModel.SecondaryColor};"">Recommandations</h3>
                <ul style=""line-height: 2;"">
                    {suggestionsHtml}
                </ul>
            </div>
            
            <div class=""footer"">
                <p style=""font-size: 1.5rem; font-weight: 600;"">Propulsé par SponsorPulse</p>
                <p style=""font-size: 0.875rem;"">Généré le {generatedDate}</p>
            </div>
        </div>
    </div>
</body>
</html>";

        return await Task.FromResult(html);
    }

    public async Task<ReportModel> BuildReportModelAsync(
        Event @event,
        PdfCustomizationOptions options
    )
    {
        var storytellingSlides = await GetStorytellingSlidesAsync(@event);
        var storytellingText = string.Join(
            Environment.NewLine + Environment.NewLine,
            storytellingSlides
                .Where(slide => !string.IsNullOrWhiteSpace(slide.Contenu))
                .Select(slide => $"{slide.Titre}\n{slide.Contenu}")
        );
        var recommendationSlides = storytellingSlides
            .Where(slide =>
                slide.Titre.Contains("recommand", StringComparison.OrdinalIgnoreCase)
                || slide.Titre.Contains("enseignement", StringComparison.OrdinalIgnoreCase)
                || slide.Titre.Contains("action", StringComparison.OrdinalIgnoreCase)
            )
            .Select(slide => $"{slide.Titre}: {slide.Contenu}")
            .ToList();

        return new ReportModel
        {
            CompanyName = options.CompanyName,
            LogoUrl = options.LogoUrl,
            PrimaryColor = options.PrimaryColor,
            SecondaryColor = options.SecondaryColor,
            EventName = @event.Name,
            EventDescription = @event.Description,
            EventDate = @event.Date,
            EventPlatform = @event.StreamPlatform,
            HasViewerCount = @event.ViewerCount.HasValue,
            HasPeakViewers = @event.PeakViewers.HasValue,
            HasStreamDuration = @event.StreamDuration.HasValue,
            TwitchMetrics = new TwitchMetrics
            {
                ViewerCount = @event.ViewerCount ?? 0,
                PeakViewers = @event.PeakViewers ?? 0,
                StreamDuration = @event.StreamDuration ?? TimeSpan.Zero,
                StartedAt = @event.StartedAt.HasValue
                    ? new DateTimeOffset(
                        DateTime.SpecifyKind(@event.StartedAt.Value, DateTimeKind.Utc)
                    )
                    : DateTimeOffset.MinValue,
                GameName = @event.GameName ?? "N/A",
            },
            TwitterAnalytics = @event.TwitterAnalytics,
            StorytellingText = storytellingText,
            StorytellingSlides = storytellingSlides,
            AnalysisText = storytellingText,
            OpportunitiesMissed = recommendationSlides,
            Suggestions = recommendationSlides,
            PhotoUrls = @event.Media.Select(m => m.Url).Take(5).ToList(),
        };
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> LoadImagesAsync(ReportModel model)
    {
        var urls = model
            .StorytellingSlides.Select(slide => slide.ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in urls)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    images[url] = await _httpClient.GetByteArrayAsync(uri);
            }
            catch (HttpRequestException)
            {
                // An unavailable optional image must not prevent PDF generation.
            }
        }

        return images;
    }

    private async Task<List<StorytellingReponse.Slide>> GetStorytellingSlidesAsync(Event @event)
    {
        await using var analyticsContext = await _analyticsDbFactory.CreateDbContextAsync();
        var analysis = await analyticsContext
            .StorytellingAnalyses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.EventId == @event.Id);
        if (analysis is null)
            return new List<StorytellingReponse.Slide>();

        return JsonSerializer.Deserialize<StorytellingReponse>(analysis.ResponseJson)?.Slides
            ?? new List<StorytellingReponse.Slide>();
    }
}
