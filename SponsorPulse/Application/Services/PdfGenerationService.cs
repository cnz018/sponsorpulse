namespace SponsorPulse.Application.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using SponsorPulse.Application.Services.Pdf;
using SponsorPulse.Domain.Entities;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Models.Twitch;
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
        var pages = new List<string>
        {
            $"""
                <section class="a4-page cover"><div class="brand">VSpike</div><div class="cover-main">
                  <p class="eyebrow">Rapport d'impact sponsoring</p><h1>{Encode(
                    reportModel.EventName
                )}</h1>
                  <p>{Encode(PdfTextSanitizer.Sanitize(reportModel.EventDescription))}</p>
                  <div class="meta"><span>{reportModel.EventDate:dd/MM/yyyy}</span><span>{Encode(
                    reportModel.EventPlatform
                )}</span></div>
                  <p>Préparé pour {Encode(reportModel.CompanyName)}</p>
                </div></section>
                """,
        };

        if (reportModel.HasStreamData)
            pages.Add(BuildStreamPreview(reportModel));
        if (reportModel.HasSocialData)
            pages.Add(BuildSocialPreview(reportModel));
        if (reportModel.HasStreamData && reportModel.HasSocialData)
            pages.Add(BuildSynergyPreview(reportModel));
        pages.Add(BuildSponsorValuePreview(reportModel));
        pages.Add(BuildRecommendationsPreview(reportModel));

        var pageCount = pages.Count;
        var pageHtml = string.Concat(
            pages.Select(
                (page, index) =>
                    page.Replace(
                        "</section>",
                        $"<footer><span>Propulsé par VSpike</span><span>Page {index + 1} sur {pageCount}</span></footer></section>",
                        StringComparison.Ordinal
                    )
            )
        );
        var primaryColor = PdfTextSanitizer.NormalizeColor(reportModel.PrimaryColor, "#1E3A8A");
        var secondaryColor = PdfTextSanitizer.NormalizeColor(reportModel.SecondaryColor, "#06B6D4");

        return $$"""
<!DOCTYPE html>
<html lang="fr">
<head>
    <meta charset="utf-8" />
    <title>Rapport d'Impact Sponsoring - {{Encode(options.CompanyName)}}</title>
    <style>
        @page { size: A4 portrait; margin: 0; }
        * { box-sizing: border-box; }
        body { margin: 0; color: #0f172a; font: 10pt Arial, sans-serif; background: #f1f5f9; }
        .a4-page { position: relative; width: 210mm; height: 297mm; padding: 15mm; margin: 12mm auto;
          overflow: hidden; background: #fff; page-break-after: always; }
        .a4-page:last-child { page-break-after: auto; }
        h1 { color: {{primaryColor}}; font-size: 28pt; margin: 22mm 0 6mm; }
        h2 { color: {{primaryColor}}; font-size: 22pt; margin: 0 0 10mm; }
        h3 { margin: 0 0 4mm; }
        p { white-space: pre-line; line-height: 1.55; }
        .brand, .eyebrow { color: {{secondaryColor}}; font-weight: 700; letter-spacing: 1.4px; text-transform: uppercase; }
        .cover { display: flex; flex-direction: column; justify-content: space-between; }
        .cover-main { margin: auto 0; }
        .meta, .cards { display: flex; gap: 5mm; margin: 8mm 0; }
        .meta span { padding: 3mm 4mm; background: #f1f5f9; border-radius: 3mm; }
        .card { flex: 1; padding: 6mm; border: 1px solid #e2e8f0; border-top: 2mm solid {{primaryColor}}; border-radius: 3mm; }
        .label { color: #64748b; font-size: 9pt; }
        .value { display: block; color: {{primaryColor}}; font-size: 25pt; font-weight: 800; margin-top: 2mm; }
        article { padding: 4mm; margin: 3mm 0; background: #f8fafc; border-radius: 2mm; }
        footer { position: absolute; bottom: 8mm; left: 15mm; right: 15mm; display: flex; justify-content: space-between;
          padding-top: 3mm; border-top: 1px solid #e2e8f0; color: #64748b; font-size: 8pt; }
        @media print { body { background: #fff; } .a4-page { margin: 0; } }
    </style>
</head>
<body>
    {pageHtml}
</body>
</html>
""";
    }

    public async Task<ReportModel> BuildReportModelAsync(
        Event @event,
        PdfCustomizationOptions options
    )
    {
        await using var analyticsContext = await _analyticsDbFactory.CreateDbContextAsync();
        var storytellingAnalysis = await analyticsContext
            .StorytellingAnalyses.AsNoTracking()
            .FirstOrDefaultAsync(item => item.EventId == @event.Id);
        var storytellingSlides =
            JsonSerializer
                .Deserialize<StorytellingReponse>(storytellingAnalysis?.ResponseJson ?? "{}")
                ?.Slides ?? [];
        // SQLite ne peut pas trier sur DateTimeOffset : tri côté client.
        var streamSnapshots = (
            await analyticsContext
                .TwitchStreamSnapshots.AsNoTracking()
                .Where(item => item.EventId == @event.Id)
                .ToListAsync()
        )
            .OrderBy(item => item.CapturedAt)
            .ThenBy(item => item.Id)
            .ToList();
        var socialPosts = await analyticsContext
            .SocialPosts.AsNoTracking()
            .Where(item => item.EventId == @event.Id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ReportSocialPost(
                item.Platform,
                item.AuthorName,
                item.ContentText,
                item.Url,
                item.CreatedAt,
                item.ImpressionsCount,
                item.LikesCount + item.SharesCount + item.CommentsCount,
                item.AuthorHandle,
                item.LikesCount,
                item.SharesCount,
                item.CommentsCount
            ))
            .ToListAsync();
        var storytellingText = string.Join(
            Environment.NewLine + Environment.NewLine,
            storytellingSlides
                .Where(slide => !string.IsNullOrWhiteSpace(slide.Contenu))
                .Select(slide => $"{slide.Titre}\n{slide.Contenu}")
        );
        var recommendationSlides = storytellingSlides
            .Where(slide => slide.Type == StorytellingSlideType.Recommendations)
            .SelectMany(slide => SplitBullets(slide.Contenu))
            .ToList();

        var hasViewerCount = @event.ViewerCount.HasValue;
        var hasPeakViewers = @event.PeakViewers.HasValue || streamSnapshots.Count > 0;
        var hasStreamDuration = @event.StreamDuration.HasValue;
        var latestSnapshot = streamSnapshots.LastOrDefault();
        var hasStreamData = hasViewerCount || hasPeakViewers || hasStreamDuration;

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
            HasViewerCount = hasViewerCount || latestSnapshot is not null,
            HasPeakViewers = hasPeakViewers,
            HasStreamDuration = hasStreamDuration,
            TwitchMetrics = hasStreamData
                ? new TwitchMetrics
                {
                    ViewerCount = @event.ViewerCount ?? latestSnapshot?.ViewerCount ?? 0,
                    PeakViewers =
                        @event.PeakViewers
                        ?? (
                            streamSnapshots.Count > 0
                                ? streamSnapshots.Max(snapshot => snapshot.PeakViewerCount)
                                : 0
                        ),
                    StreamDuration = @event.StreamDuration ?? TimeSpan.Zero,
                    StartedAt = @event.StartedAt.HasValue
                        ? new DateTimeOffset(
                            DateTime.SpecifyKind(@event.StartedAt.Value, DateTimeKind.Utc)
                        )
                        : latestSnapshot?.StreamStartedAt ?? DateTimeOffset.MinValue,
                    GameName = @event.GameName ?? latestSnapshot?.GameName ?? string.Empty,
                    AvgViewers =
                        streamSnapshots.Count > 0
                            ? streamSnapshots.Average(snapshot => snapshot.ViewerCount)
                            : @event.ViewerCount ?? 0,
                }
                : null,
            TwitterAnalytics = @event.TwitterAnalytics,
            StreamSnapshots = streamSnapshots,
            SocialPosts = socialPosts,
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
            .Append(model.LogoUrl)
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
                {
                    var bytes = await _httpClient.GetByteArrayAsync(uri);
                    if (IsDecodableImage(bytes))
                        images[url] = bytes;
                }
            }
            catch (HttpRequestException)
            {
                // An unavailable optional image must not prevent PDF generation.
            }
        }

        return images;
    }

    private static bool IsDecodableImage(byte[] bytes)
    {
        try
        {
            using var image = QuestPDF.Infrastructure.Image.FromBinaryData(bytes);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static List<string> SplitBullets(string content) =>
        PdfTextSanitizer
            .Sanitize(content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
                System.Text.RegularExpressions.Regex.Replace(
                    line,
                    @"^(?:[-•*–]|\d+[.)])\s*",
                    string.Empty
                )
            )
            .Where(line => line.Length > 0)
            .ToList();

    private static string BuildStreamPreview(ReportModel model) =>
        $$"""
            <section class="a4-page"><p class="eyebrow">Audience live · {{Encode(
                model.EventPlatform
            )}}</p><h2>Métriques du stream</h2><div class="cards">
            {{(
                model.HasPeakViewers
                    ? MetricCard(
                        "Pic d'audience",
                        PdfTextSanitizer.FormatNumber(model.TwitchMetrics!.PeakViewers)
                    )
                    : ""
            )}}
            {{(
                model.HasViewerCount
                    ? MetricCard(
                        "Viewers moyens / dernière mesure",
                        PdfTextSanitizer.FormatNumber(model.TwitchMetrics!.AvgViewers)
                    )
                    : ""
            )}}
            {{(
                model.HasStreamDuration
                    ? MetricCard(
                        "Durée",
                        PdfTextSanitizer.FormatDuration(model.TwitchMetrics!.StreamDuration)
                    )
                    : ""
            )}}
            </div>{{BuildSlides(model, StorytellingSlideType.StreamMetrics)}}</section>
            """;

    private static string BuildSocialPreview(ReportModel model)
    {
        var analytics = model.TwitterAnalytics;
        var impressions =
            analytics?.EstimatedImpressions ?? model.SocialPosts.Sum(post => post.Impressions);
        var engagement =
            analytics?.TotalEngagement ?? model.SocialPosts.Sum(post => post.Engagement);
        var postCount = analytics?.TotalTweets ?? model.SocialPosts.Count;
        var posts = string.Concat(
            model
                .SocialPosts.OrderByDescending(post => post.Engagement)
                .Take(1)
                .Select(post =>
                    $"<article><strong>Publication la plus engageante · {Encode(PlatformIcons.Label(post.Platform))}</strong><p>{Encode(PdfTextSanitizer.Sanitize(post.Text))}</p><span>{PdfTextSanitizer.FormatNumber(post.Engagement)} interactions</span></article>"
                )
        );

        return $$"""
            <section class="a4-page"><p class="eyebrow">Réseaux sociaux</p><h2>Métriques sociales</h2><div class="cards">
            {{MetricCard("Impressions", PdfTextSanitizer.FormatNumber(impressions))}}
            {{MetricCard("Engagements", PdfTextSanitizer.FormatNumber(engagement))}}
            {{(
                analytics is null
                    ? ""
                    : MetricCard(
                        "Taux moyen",
                        PdfTextSanitizer.FormatPercent(analytics.EngagementRate)
                    )
            )}}
            {{MetricCard("Publications", PdfTextSanitizer.FormatNumber(postCount))}}
            </div>{{BuildSlides(
                model,
                StorytellingSlideType.SocialNetworkMetrics
            )}}{{posts}}</section>
            """;
    }

    private static string BuildSynergyPreview(ReportModel model) =>
        $$"""
            <section class="a4-page"><p class="eyebrow">Stream + réseaux sociaux</p><h2>Synergie des canaux</h2>
            {{BuildSlides(model, StorytellingSlideType.TwitchTwitterSynergy)}}
            <p>Comparez les instants de forte audience aux publications et interactions sociales relevées pendant l'événement.</p>
            </section>
            """;

    private static string BuildSponsorValuePreview(ReportModel model)
    {
        var analytics = model.TwitterAnalytics;
        var details = analytics is null
            ? "<p>La valorisation sera disponible dès que des données sociales auront été collectées.</p>"
            : $$"""
                <div class="cards">{{MetricCard(
                    "CPM effectif",
                    PdfTextSanitizer.FormatCurrency(analytics.CPM)
                )}}
                {{MetricCard(
                    "Multiplicateur viral",
                    $"{PdfTextSanitizer.FormatFrench(analytics.ViralMultiplier, "N2")}x"
                )}}
                {{MetricCard(
                    "Portée unique",
                    PdfTextSanitizer.FormatNumber(analytics.UniqueReach)
                )}}</div>
                <article>Impressions de base : {{PdfTextSanitizer.FormatNumber(
                    analytics.EstimatedImpressions
                )}}<br/>
                Amplification virale : {{PdfTextSanitizer.FormatFrench(
                    analytics.ViralMultiplier,
                    "N2"
                )}}x<br/>
                Valeur équivalente publicitaire : {{PdfTextSanitizer.FormatCurrency(
                    analytics.AdValueEquivalent
                )}}</article>
                """;
        return $$"""
            <section class="a4-page"><p class="eyebrow">Résultats sponsor</p><h2>Valorisation de la campagne</h2>
            <div class="card"><span class="label">Valeur équivalente publicitaire (AVE)</span>
            <span class="value">{{(
                analytics is null
                    ? "—"
                    : PdfTextSanitizer.FormatCurrency(analytics.AdValueEquivalent)
            )}}</span></div>
            {{details}}{{BuildSlides(model, StorytellingSlideType.SponsorValue)}}</section>
            """;
    }

    private static string BuildRecommendationsPreview(ReportModel model)
    {
        var recommendations =
            model.Suggestions.Count > 0
                ? model.Suggestions
                : model
                    .StorytellingSlides.Where(slide =>
                        slide.Type == StorytellingSlideType.Recommendations
                    )
                    .Select(slide => $"{slide.Titre}: {slide.Contenu}")
                    .ToList();
        var content = string.Concat(
            recommendations
                .Take(8)
                .Select(
                    (item, index) =>
                        $"<article><strong>{index + 1}.</strong> {Encode(PdfTextSanitizer.Sanitize(item))}</article>"
                )
        );
        if (string.IsNullOrWhiteSpace(content))
            content = BuildSlides(
                model,
                StorytellingSlideType.EventSummary,
                StorytellingSlideType.ExecutiveSummary
            );
        if (string.IsNullOrWhiteSpace(content))
            content =
                "<p>Les indicateurs disponibles constituent le bilan de référence pour les prochaines activations.</p>";

        return $$"""
            <section class="a4-page"><p class="eyebrow">Suite à donner</p><h2>Recommandations &amp; bilan</h2>
            {{content}}</section>
            """;
    }

    private static string BuildSlides(ReportModel model, params StorytellingSlideType[] types) =>
        string.Concat(
            model
                .StorytellingSlides.Where(slide => types.Contains(slide.Type))
                .Select(slide =>
                    $"<article><h3>{Encode(PdfTextSanitizer.Sanitize(slide.Titre))}</h3><p>{Encode(PdfTextSanitizer.Sanitize(slide.Contenu))}</p></article>"
                )
        );

    private static string MetricCard(string label, string value) =>
        $"<div class=\"card\"><span class=\"label\">{Encode(label)}</span><span class=\"value\">{Encode(value)}</span></div>";

    private static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
