using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;
using SponsorPulse.Domain.Models.Twitch;

namespace SponsorPulse.Application.Services.Pdf;

public sealed class QuestPdfReportDocument(
    ReportModel model,
    IReadOnlyDictionary<string, byte[]> images
) : IDocument
{
    private const string FallbackPrimary = "#1E3A8A";
    private const string FallbackSecondary = "#06B6D4";
    private readonly ReportModel _model = model;
    private readonly IReadOnlyDictionary<string, byte[]> _images = images;
    private string Primary => PdfTextSanitizer.NormalizeColor(_model.PrimaryColor, FallbackPrimary);
    private string Secondary =>
        PdfTextSanitizer.NormalizeColor(_model.SecondaryColor, FallbackSecondary);

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        var pages = new List<Action<IContainer>> { ComposeCover };
        if (_model.HasStreamData)
            pages.Add(ComposeStreamMetrics);
        if (_model.HasSocialData)
            pages.Add(ComposeSocialMetrics);
        if (_model.HasStreamData && _model.HasSocialData)
            pages.Add(ComposeSynergy);
        pages.Add(ComposeSponsorValue);
        pages.Add(ComposeRecommendations);

        foreach (var compose in pages)
            ComposePage(container, compose);
    }

    private void ComposePage(IDocumentContainer container, Action<IContainer> compose)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(15, Unit.Millimetre);
            page.DefaultTextStyle(style =>
                style.FontFamily("Lato", "Noto Emoji").FontSize(10).FontColor("#0F172A")
            );
            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(8).Element(compose);
            page.Footer()
                .PaddingTop(5)
                .BorderTop(1)
                .BorderColor("#E2E8F0")
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text(text =>
                        {
                            text.Span("Propulsé par VSpike")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        });
                    row.AutoItem()
                        .Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                            text.CurrentPageNumber();
                            text.Span(" sur ").FontSize(8).FontColor(Colors.Grey.Darken1);
                            text.TotalPages();
                        });
                });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container
            .PaddingBottom(6)
            .BorderBottom(1)
            .BorderColor(Secondary)
            .Row(row =>
            {
                row.RelativeItem()
                    .Text(text =>
                    {
                        text.Span("VSPIKE").Bold().FontSize(9).FontColor(Primary);
                        if (!string.IsNullOrWhiteSpace(_model.CompanyName))
                            text.Span($"  ·  {Clean(_model.CompanyName)}")
                                .FontSize(9)
                                .FontColor("#64748B");
                    });

                if (
                    !string.IsNullOrWhiteSpace(_model.LogoUrl)
                    && _images.TryGetValue(_model.LogoUrl, out var logo)
                )
                    row.ConstantItem(48).Height(28).Image(logo).FitArea();
            });
    }

    private void ComposeCover(IContainer container)
    {
        container.Column(column =>
        {
            column
                .Item()
                .PaddingTop(30)
                .Background("#F8FAFC")
                .BorderLeft(6)
                .BorderColor(Primary)
                .Padding(40)
                .Column(hero =>
                {
                    hero.Spacing(24);
                    hero.Item()
                        .Text("RAPPORT D'IMPACT SPONSORING")
                        .Bold()
                        .FontSize(11)
                        .FontColor(Secondary);
                    hero.Item()
                        .Text(Clean(_model.EventName))
                        .Bold()
                        .FontSize(36)
                        .FontColor(Primary);
                    hero.Item().Text(Clean(_model.CompanyName)).FontSize(16).FontColor("#475569");
                });

            column
                .Item()
                .Height(290)
                .AlignMiddle()
                .PaddingVertical(30)
                .Column(body =>
                {
                    if (!string.IsNullOrWhiteSpace(_model.EventDescription))
                        body.Item()
                            .Text(Truncate(Clean(_model.EventDescription), 900))
                            .FontSize(13)
                            .LineHeight(1.6f);
                });

            column
                .Item()
                .Row(row =>
                {
                    row.Spacing(14);
                    row.RelativeItem()
                        .MinHeight(80)
                        .Element(content =>
                            InfoCard(
                                content,
                                "DATE DE L'ÉVÉNEMENT",
                                _model.EventDate.ToString(
                                    "dd MMMM yyyy",
                                    CultureInfo.GetCultureInfo("fr-FR")
                                )
                            )
                        );
                    row.RelativeItem()
                        .Element(content => PlatformsCard(content, "PLATEFORMES ANALYSÉES"));
                });
        });
    }

    private void ComposeStreamMetrics(IContainer container)
    {
        var metrics = _model.TwitchMetrics;
        container.Column(column =>
        {
            column.Spacing(13);
            SectionTitle(column.Item(), "Audience en direct", "PERFORMANCE STREAM");
            PlatformBadges(column.Item(), [_model.EventPlatform]);

            column
                .Item()
                .Row(row =>
                {
                    row.Spacing(10);
                    if (_model.HasPeakViewers && metrics is not null)
                        HeroCard(
                            row.RelativeItem(),
                            "Pic de viewers",
                            PdfTextSanitizer.FormatNumber(metrics.PeakViewers)
                        );
                    if (_model.HasViewerCount && metrics is not null)
                        HeroCard(
                            row.RelativeItem(),
                            _model.StreamSnapshots.Count > 0 ? "Viewers moyens" : "Viewers mesurés",
                            PdfTextSanitizer.FormatNumber(metrics.AvgViewers)
                        );
                    if (_model.HasStreamDuration && metrics is not null)
                        HeroCard(
                            row.RelativeItem(),
                            "Durée totale",
                            PdfTextSanitizer.FormatDuration(metrics.StreamDuration)
                        );
                });

            if (_model.StreamSnapshots.Count > 1)
            {
                Subheading(column.Item(), "Évolution de l'audience");
                column.Item().Svg(BuildViewerChart(_model.StreamSnapshots));
            }

            ComposeSlides(column, StorytellingSlideType.StreamMetrics);
        });
    }

    private void ComposeSocialMetrics(IContainer container)
    {
        var analytics = _model.TwitterAnalytics;
        var impressions =
            analytics?.EstimatedImpressions ?? _model.SocialPosts.Sum(post => post.Impressions);
        var engagement =
            analytics?.TotalEngagement ?? _model.SocialPosts.Sum(post => post.Engagement);
        var postCount = analytics?.TotalTweets ?? _model.SocialPosts.Count;

        container.Column(column =>
        {
            column.Spacing(12);
            SectionTitle(column.Item(), "Rayonnement social", "RÉSEAUX SOCIAUX");
            ComposePlatformBadges(column.Item());
            column
                .Item()
                .Row(row =>
                {
                    row.Spacing(10);
                    HeroCard(
                        row.RelativeItem(),
                        "Impressions totales",
                        PdfTextSanitizer.FormatNumber(impressions),
                        30
                    );
                    HeroCard(
                        row.RelativeItem(),
                        "Engagement total",
                        PdfTextSanitizer.FormatNumber(engagement),
                        30
                    );
                });
            column
                .Item()
                .Row(row =>
                {
                    row.Spacing(10);
                    if (analytics is not null)
                        HeroCard(
                            row.RelativeItem(),
                            "Taux d'engagement moyen",
                            PdfTextSanitizer.FormatPercent(analytics.EngagementRate),
                            20
                        );
                    HeroCard(
                        row.RelativeItem(),
                        "Publications",
                        PdfTextSanitizer.FormatNumber(postCount),
                        20
                    );
                });

            if (analytics is not null)
            {
                column
                    .Item()
                    .Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem()
                            .Element(content =>
                                ComposeSentiment(content, analytics.SentimentScore)
                            );
                        if (analytics.LanguageRatio.Count > 0)
                            row.RelativeItem()
                                .Element(content =>
                                    ComposeLanguageDistribution(content, analytics.LanguageRatio)
                                );
                    });
            }

            ComposeTopPost(column.Item());
            ComposeSlides(column, StorytellingSlideType.SocialNetworkMetrics);
        });
    }

    private void ComposeSynergy(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            SectionTitle(
                column.Item(),
                "Quand les canaux se répondent",
                "SYNERGIE STREAM & SOCIAL"
            );
            if (_model.StreamSnapshots.Count > 1 && _model.SocialPosts.Count > 0)
            {
                column.Item().Svg(BuildSynergyChart());
                column
                    .Item()
                    .Text(
                        "Courbe : viewers simultanés · Barres : publications capturées par intervalle"
                    )
                    .FontSize(8)
                    .FontColor("#64748B");
                var insight = GetSynergyInsight();
                if (!string.IsNullOrWhiteSpace(insight))
                    InsightCard(column.Item(), insight);
            }
            else
            {
                InsightCard(
                    column.Item(),
                    "Les données temporelles détaillées des publications ne sont pas disponibles pour calculer une corrélation fiable."
                );
            }
            ComposeSlides(column, StorytellingSlideType.TwitchTwitterSynergy);
        });
    }

    private void ComposeSponsorValue(IContainer container)
    {
        var analytics = _model.TwitterAnalytics;
        container.Column(column =>
        {
            column.Spacing(15);
            SectionTitle(column.Item(), "Valeur créée pour le sponsor", "VALORISATION");
            if (analytics is null)
            {
                InsightCard(
                    column.Item(),
                    "La valorisation publicitaire sera disponible dès que les données d'exposition sociale auront été collectées."
                );
                ComposeSlides(column, StorytellingSlideType.SponsorValue);
                return;
            }

            column
                .Item()
                .Background("#F1F5F9")
                .Border(1)
                .BorderColor(Primary)
                .Padding(22)
                .Column(hero =>
                {
                    hero.Spacing(8);
                    hero.Item()
                        .Text("VALEUR ÉQUIVALENTE PUBLICITAIRE (AVE)")
                        .Bold()
                        .FontSize(9)
                        .FontColor("#475569");
                    hero.Item()
                        .Text(PdfTextSanitizer.FormatCurrency(analytics.AdValueEquivalent))
                        .ExtraBold()
                        .FontSize(42)
                        .FontColor(Primary);
                });

            column
                .Item()
                .Row(row =>
                {
                    row.Spacing(10);
                    HeroCard(
                        row.RelativeItem(),
                        "CPM effectif",
                        PdfTextSanitizer.FormatCurrency(analytics.CPM),
                        17
                    );
                    HeroCard(
                        row.RelativeItem(),
                        "Multiplicateur viral",
                        $"{PdfTextSanitizer.FormatFrench(analytics.ViralMultiplier, "N2")}x",
                        17
                    );
                    HeroCard(
                        row.RelativeItem(),
                        "Portée unique",
                        PdfTextSanitizer.FormatNumber(analytics.UniqueReach),
                        17
                    );
                });

            Subheading(column.Item(), "Ventilation de la valeur");
            KeyValueTable(
                column.Item(),
                [
                    ("Impressions estimées", PdfTextSanitizer.FormatNumber(analytics.EstimatedImpressions)),
                    ("Engagement total", PdfTextSanitizer.FormatNumber(analytics.TotalEngagement)),
                    ("ROI estimé", PdfTextSanitizer.FormatCurrency(analytics.EstimatedROI)),
                    ("Part de voix", PdfTextSanitizer.FormatPercent(analytics.ShareOfVoice)),
                ]
            );
            ComposeSlides(column, StorytellingSlideType.SponsorValue);
        });
    }

    private void ComposeRecommendations(IContainer container)
    {
        var recommendations =
            _model.Suggestions.Count > 0
                ? _model.Suggestions
                : _model
                    .StorytellingSlides.Where(slide =>
                        slide.Type == StorytellingSlideType.Recommendations
                    )
                    .Select(slide => $"{slide.Titre} : {slide.Contenu}")
                    .ToList();
        recommendations = recommendations.Take(8).ToList();

        container.Column(column =>
        {
            column.Spacing(12);
            SectionTitle(
                column.Item(),
                "Transformer l'impact en action",
                "RECOMMANDATIONS & BILAN"
            );

            var summary =
                _model.StorytellingSlides.FirstOrDefault(slide =>
                    slide.Type == StorytellingSlideType.EventSummary
                )
                ?? _model.StorytellingSlides.FirstOrDefault(slide =>
                    slide.Type == StorytellingSlideType.ExecutiveSummary
                );
            if (summary is not null && !string.IsNullOrWhiteSpace(summary.Contenu))
            {
                Subheading(column.Item(), "Synthèse globale");
                InsightCard(column.Item(), Clean(summary.Contenu));
            }
            else if (!string.IsNullOrWhiteSpace(_model.AnalysisText) && recommendations.Count == 0)
            {
                Subheading(column.Item(), "Bilan");
                InsightCard(column.Item(), Truncate(Clean(_model.AnalysisText), 1000));
            }

            if (recommendations.Count > 0)
            {
                Subheading(column.Item(), "Recommandations");
                for (var index = 0; index < recommendations.Count; index += 2)
                {
                    var rowItems = recommendations.Skip(index).Take(2).ToList();
                    column
                        .Item()
                        .Row(row =>
                        {
                            row.Spacing(10);
                            for (var itemIndex = 0; itemIndex < rowItems.Count; itemIndex++)
                            {
                                var recommendation = rowItems[itemIndex];
                                var number = index + itemIndex + 1;
                                row.RelativeItem()
                                    .Element(content =>
                                        RecommendationCard(content, number, recommendation)
                                    );
                            }
                            if (rowItems.Count == 1)
                                row.RelativeItem();
                        });
                }
            }
            else
            {
                InsightCard(
                    column.Item(),
                    "Les indicateurs présentés dans ce rapport constituent le bilan de référence pour préparer les prochaines activations."
                );
            }
        });
    }

    private void ComposeSlides(ColumnDescriptor column, params StorytellingSlideType[] types)
    {
        foreach (
            var slide in _model
                .StorytellingSlides.Where(slide => types.Contains(slide.Type))
                .Take(2)
        )
        {
            if (!string.IsNullOrWhiteSpace(slide.Titre))
                Subheading(column.Item(), Clean(slide.Titre));
            if (!string.IsNullOrWhiteSpace(slide.Contenu))
                column.Item().Text(Truncate(Clean(slide.Contenu), 1100)).LineHeight(1.4f);
            if (
                slide.EstVisualisation
                && slide.DonneesVisualisation is not null
                && string.IsNullOrWhiteSpace(slide.Contenu)
            )
                column.Item().Text("Données de visualisation intégrées.").FontColor("#64748B");
            if (
                !string.IsNullOrWhiteSpace(slide.ImageUrl)
                && _images.TryGetValue(slide.ImageUrl, out var imageBytes)
            )
                column.Item().MaxHeight(90).Image(imageBytes).FitArea();
        }
    }

    private void ComposePlatformBadges(IContainer container)
    {
        var platforms = _model
            .SocialPosts.Select(post => PlatformIcons.Label(post.Platform))
            .Distinct()
            .ToList();
        if (platforms.Count == 0 && _model.TwitterAnalytics is not null)
            platforms.Add(PlatformIcons.Label(SocialPlatform.Twitter));
        PlatformBadges(container, platforms);
    }

    private void ComposeSentiment(IContainer container, double score)
    {
        var normalized = Math.Clamp((score + 1) / 2, 0, 1);
        container
            .Background("#F8FAFC")
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("Sentiment des publications").Bold().FontSize(9);
                column.Item().Height(24).Svg(BuildSentimentGauge(normalized));
                column
                    .Item()
                    .Text($"Indice : {PdfTextSanitizer.FormatFrench(score, "N2")}")
                    .FontSize(8)
                    .FontColor("#475569");
            });
    }

    private string BuildSentimentGauge(double normalized)
    {
        var markerPosition = 8 + Math.Clamp(normalized, 0, 1) * 184;
        return SvgImage(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"24\" viewBox=\"0 0 200 24\"><defs><linearGradient id=\"g\"><stop offset=\"0%\" stop-color=\"#EF4444\"/><stop offset=\"50%\" stop-color=\"#FBBF24\"/><stop offset=\"100%\" stop-color=\"#22C55E\"/></linearGradient></defs><rect x=\"8\" y=\"8\" width=\"184\" height=\"8\" rx=\"4\" fill=\"url(#g)\"/><circle cx=\"{markerPosition:0.0}\" cy=\"12\" r=\"6\" fill=\"#FFFFFF\" stroke=\"{Primary}\" stroke-width=\"3\"/></svg>"
        );
    }

    private static readonly string[] LanguagePalette =
    [
        "#1E3A8A",
        "#06B6D4",
        "#E4405F",
        "#9146FF",
        "#F59E0B",
    ];

    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["fr"] = "Français",
        ["en"] = "Anglais",
        ["es"] = "Espagnol",
        ["pt"] = "Portugais",
        ["de"] = "Allemand",
        ["it"] = "Italien",
        ["nl"] = "Néerlandais",
        ["ja"] = "Japonais",
        ["ko"] = "Coréen",
        ["zh"] = "Chinois",
        ["ar"] = "Arabe",
        ["ru"] = "Russe",
        ["tr"] = "Turc",
        ["pl"] = "Polonais",
        ["sv"] = "Suédois",
        ["th"] = "Thaï",
        ["id"] = "Indonésien",
        ["hi"] = "Hindi",
        ["und"] = "Indéterminée",
        ["qme"] = "Autre",
        ["qht"] = "Autre",
        ["zxx"] = "Autre",
    };

    private static string LanguageName(string code)
    {
        var key = code.Trim().ToLowerInvariant();
        if (LanguageNames.TryGetValue(key, out var name))
            return name;
        try
        {
            var native = CultureInfo.GetCultureInfo(key).NativeName;
            return native.Length > 0 ? char.ToUpperInvariant(native[0]) + native[1..] : key;
        }
        catch (CultureNotFoundException)
        {
            return key.ToUpperInvariant();
        }
    }

    private void ComposeLanguageDistribution(
        IContainer container,
        IReadOnlyDictionary<string, double> languageRatio
    )
    {
        var ordered = languageRatio
            .Where(entry => entry.Value > 0)
            .OrderByDescending(entry => entry.Value)
            .ToList();
        var scale = ordered.Sum(entry => entry.Value) <= 1.5 ? 100d : 1d;
        var segments = ordered
            .Take(LanguagePalette.Length)
            .Select(
                (entry, index) =>
                    (Label: LanguageName(entry.Key), Value: entry.Value * scale, Color: LanguagePalette[index])
            )
            .ToList();
        var others = ordered.Skip(LanguagePalette.Length).Sum(entry => entry.Value) * scale;
        if (others > 0)
            segments.Add(("Autres", others, "#94A3B8"));

        container
            .Background("#F8FAFC")
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("Langues des publications").Bold().FontSize(9);
                if (segments.Count == 0)
                    return;

                column
                    .Item()
                    .Row(row =>
                    {
                        row.Spacing(10);
                        row.ConstantItem(80)
                            .Height(80)
                            .Svg(
                                BuildLanguageChart(
                                    segments.Select(item => (item.Value, item.Color)).ToList()
                                )
                            );
                        row.RelativeItem()
                            .AlignMiddle()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(definition =>
                                {
                                    definition.ConstantColumn(12);
                                    definition.RelativeColumn();
                                    definition.ConstantColumn(42);
                                });
                                foreach (var segment in segments)
                                {
                                    table
                                        .Cell()
                                        .Element(LegendCell)
                                        .AlignMiddle()
                                        .AlignLeft()
                                        .Width(8)
                                        .Height(8)
                                        .Background(segment.Color);
                                    table
                                        .Cell()
                                        .Element(LegendCell)
                                        .Text(segment.Label)
                                        .FontSize(8);
                                    table
                                        .Cell()
                                        .Element(LegendCell)
                                        .AlignRight()
                                        .Text($"{PdfTextSanitizer.FormatFrench(segment.Value, "N1")} %")
                                        .Bold()
                                        .FontSize(8);
                                }
                            });
                    });
            });
    }

    private static IContainer LegendCell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor("#E2E8F0").PaddingVertical(3);

    private static string BuildLanguageChart(IReadOnlyList<(double Value, string Color)> items)
    {
        var palette = items.Select(item => item.Color).ToArray();
        var values = items.Select(item => Math.Max(0, item.Value)).ToArray();
        var sum = values.Sum();
        const double circumference = 138.23;
        var offset = 0d;
        var segments = new StringBuilder();

        for (var index = 0; index < values.Length; index++)
        {
            var length = sum == 0 ? 0 : circumference * values[index] / sum;
            segments.Append(
                FormattableString.Invariant($"<circle cx=\"45\" cy=\"45\" r=\"22\" fill=\"none\" stroke=\"{palette[index]}\" stroke-width=\"12\" stroke-dasharray=\"{length:0.00} {circumference - length:0.00}\" stroke-dashoffset=\"{-offset:0.00}\" transform=\"rotate(-90 45 45)\"/>")
            );
            offset += length;
        }

        return SvgImage(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"90\" height=\"90\" viewBox=\"0 0 90 90\"><circle cx=\"45\" cy=\"45\" r=\"22\" fill=\"none\" stroke=\"#E2E8F0\" stroke-width=\"12\"/>{segments}</svg>"
        );
    }

    private sealed record TopPost(
        string Platform,
        string Author,
        string Handle,
        string Text,
        long Interactions,
        long Impressions,
        int Likes,
        int Shares,
        int Comments
    );

    private List<TopPost> GetTopPosts()
    {
        var posts = _model
            .SocialPosts.OrderByDescending(post => post.Engagement)
            .Take(3)
            .Select(post => new TopPost(
                PlatformIcons.Label(post.Platform),
                post.AuthorName,
                post.AuthorHandle,
                post.Text,
                post.Engagement,
                post.Impressions,
                post.Likes,
                post.Shares,
                post.Comments
            ))
            .ToList();
        if (posts.Count > 0 || _model.TwitterAnalytics is null)
            return posts;

        return _model
            .TwitterAnalytics.TopTweets.OrderByDescending(tweet =>
                tweet.Likes + tweet.Retweets + tweet.Replies
            )
            .Take(3)
            .Select(tweet => new TopPost(
                "X",
                tweet.AuthorName,
                tweet.AuthorHandle,
                tweet.Text,
                tweet.Likes + tweet.Retweets + tweet.Replies,
                tweet.Impressions,
                tweet.Likes,
                tweet.Retweets,
                tweet.Replies
            ))
            .ToList();
    }

    private void ComposeTopPost(IContainer container)
    {
        var posts = GetTopPosts();
        if (posts.Count == 0)
            return;

        container.Column(column =>
        {
            column.Spacing(8);
            Subheading(column.Item(), "Top 3 des publications · classées par engagement");
            for (var index = 0; index < posts.Count; index++)
            {
                var rank = index + 1;
                var post = posts[index];
                column.Item().Element(content => TopPostCard(content, rank, post));
            }

            Subheading(column.Item(), "Détail des publications");
            column.Item().Element(content => ComposeTopPostsTable(content, posts));
        });
    }

    private void TopPostCard(IContainer container, int rank, TopPost post)
    {
        var handle = post.Handle.Trim();
        if (handle.Length > 0 && !handle.StartsWith('@'))
            handle = $"@{handle}";

        container
            .Background("#F8FAFC")
            .BorderLeft(4)
            .BorderColor(PlatformIcons.Color(post.Platform))
            .Padding(10)
            .Row(row =>
            {
                row.ConstantItem(24)
                    .Height(24)
                    .Background(Primary)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(rank.ToString(CultureInfo.InvariantCulture))
                    .Bold()
                    .FontColor(Colors.White);
                row.RelativeItem()
                    .PaddingHorizontal(10)
                    .Column(body =>
                    {
                        body.Spacing(4);
                        body.Item()
                            .Row(header =>
                            {
                                header.Spacing(6);
                                header
                                    .AutoItem()
                                    .Element(tag => PlatformTag(tag, post.Platform));
                                header
                                    .RelativeItem()
                                    .Text(text =>
                                    {
                                        text.Span(Clean(post.Author)).Bold().FontSize(9);
                                        if (handle.Length > 0)
                                            text.Span($"  {handle}")
                                                .FontSize(8)
                                                .FontColor("#64748B");
                                    });
                            });
                        body.Item()
                            .Text(Truncate(Clean(post.Text), 160))
                            .FontSize(9)
                            .LineHeight(1.35f);
                    });
                row.ConstantItem(86)
                    .Column(metric =>
                    {
                        metric
                            .Item()
                            .AlignRight()
                            .Text(PdfTextSanitizer.FormatNumber(post.Interactions))
                            .Bold()
                            .FontSize(22)
                            .FontColor(Primary);
                        metric
                            .Item()
                            .AlignRight()
                            .Text("interactions")
                            .FontSize(7)
                            .FontColor("#64748B");
                    });
            });
    }

    private void ComposeTopPostsTable(IContainer container, IReadOnlyList<TopPost> posts)
    {
        string[] titles = ["#", "Plateforme", "Likes", "Partages", "Réponses", "Impressions", "Taux"];
        container.Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.ConstantColumn(22);
                definition.RelativeColumn(1.6f);
                definition.RelativeColumn();
                definition.RelativeColumn(1.1f);
                definition.RelativeColumn(1.1f);
                definition.RelativeColumn(1.3f);
                definition.RelativeColumn();
            });
            table.Header(header =>
            {
                for (var index = 0; index < titles.Length; index++)
                {
                    var cell = header.Cell().Element(HeaderCell);
                    (index >= 2 ? cell.AlignRight() : cell)
                        .Text(titles[index])
                        .Bold()
                        .FontSize(7)
                        .FontColor("#64748B");
                }
            });

            for (var index = 0; index < posts.Count; index++)
            {
                var post = posts[index];
                var rate = post.Impressions > 0
                    ? $"{PdfTextSanitizer.FormatFrench(post.Interactions * 100d / post.Impressions, "N2")} %"
                    : "—";
                table
                    .Cell()
                    .Element(BodyCell)
                    .Text((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Bold()
                    .FontSize(8);
                table.Cell().Element(BodyCell).Element(tag => PlatformTag(tag, post.Platform, 8));
                NumberCell(table, PdfTextSanitizer.FormatNumber(post.Likes));
                NumberCell(table, PdfTextSanitizer.FormatNumber(post.Shares));
                NumberCell(table, PdfTextSanitizer.FormatNumber(post.Comments));
                NumberCell(table, PdfTextSanitizer.FormatNumber(post.Impressions));
                NumberCell(table, rate);
            }
        });
    }

    private static void NumberCell(TableDescriptor table, string value) =>
        table.Cell().Element(BodyCell).AlignRight().Text(value).FontSize(8);

    private static IContainer HeaderCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor("#CBD5E1").PaddingVertical(4).PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor("#E2E8F0").PaddingVertical(5).PaddingHorizontal(4);

    private static void KeyValueTable(
        IContainer container,
        IReadOnlyList<(string Label, string Value)> rows
    ) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.RelativeColumn(2);
                definition.RelativeColumn();
            });
            foreach (var (label, value) in rows)
            {
                table.Cell().Element(BodyCell).Text(label).FontSize(9).FontColor("#475569");
                table.Cell().Element(BodyCell).AlignRight().Text(value).FontSize(9).Bold();
            }
        });

    private string BuildSynergyChart()
    {
        var snapshots = _model.StreamSnapshots;
        var start = snapshots[0].CapturedAt;
        var end = snapshots[^1].CapturedAt;
        var duration = Math.Max(1, (end - start).TotalSeconds);
        const int bins = 12;
        var socialCounts = new int[bins];
        foreach (var post in _model.SocialPosts)
        {
            var index = (int)
                Math.Floor((post.CreatedAt - start.DateTime).TotalSeconds / duration * bins);
            if (index >= 0 && index < bins)
                socialCounts[index]++;
        }

        var values = snapshots.Select(snapshot => (double)snapshot.ViewerCount).ToArray();
        var maxViewers = Math.Max(1, values.Max());
        var maxPosts = Math.Max(1, socialCounts.Max());
        var points = string.Join(
            " ",
            values.Select(
                (value, index) =>
                    FormattableString.Invariant($"{40 + index * 620d / Math.Max(1, values.Length - 1):0.0},{155 - value / maxViewers * 125:0.0}")
            )
        );
        var bars = string.Concat(
            socialCounts.Select(
                (count, index) =>
                    FormattableString.Invariant($"<rect x=\"{40 + index * 620d / bins:0.0}\" y=\"{155 - count / (double)maxPosts * 90:0.0}\" width=\"{Math.Max(4, 620d / bins - 4):0.0}\" height=\"{count / (double)maxPosts * 90:0.0}\" fill=\"{Secondary}\" opacity=\"0.55\"/>")
            )
        );

        return SvgImage(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"700\" height=\"190\" viewBox=\"0 0 700 190\"><rect width=\"700\" height=\"190\" fill=\"#FFFFFF\"/>{bars}<polyline points=\"{points}\" fill=\"none\" stroke=\"{Primary}\" stroke-width=\"4\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/><path d=\"M40 165H660\" stroke=\"#CBD5E1\"/></svg>"
        );
    }

    private string BuildViewerChart(IReadOnlyList<TwitchStreamSnapshot> snapshots)
    {
        var values = snapshots.Select(snapshot => (double)snapshot.ViewerCount).ToArray();
        var max = Math.Max(1, values.Max());
        var points = string.Join(
            " ",
            values.Select(
                (value, index) =>
                    FormattableString.Invariant($"{30 + index * 640d / Math.Max(1, values.Length - 1):0.0},{125 - value / max * 105:0.0}")
            )
        );
        return SvgImage(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"700\" height=\"150\" viewBox=\"0 0 700 150\"><rect width=\"700\" height=\"150\" fill=\"#FFFFFF\"/><path d=\"M30 132H670\" stroke=\"#CBD5E1\"/><polyline points=\"{points}\" fill=\"none\" stroke=\"{Primary}\" stroke-width=\"4\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>"
        );
    }

    // Les nombres des SVG doivent utiliser le point décimal, quelle que soit la culture du serveur.
    private static string SvgImage(FormattableString svg) => svg.ToString(CultureInfo.InvariantCulture);

    private string? GetSynergyInsight()
    {
        if (_model.SocialPosts.Count < 2 || _model.StreamSnapshots.Count == 0)
            return null;

        var peak = _model.StreamSnapshots.MaxBy(snapshot => snapshot.ViewerCount);
        if (peak is null)
            return null;
        var peakTime = peak.CapturedAt.DateTime;
        var afterPeak = _model
            .SocialPosts.Where(post =>
                post.CreatedAt >= peakTime && post.CreatedAt <= peakTime.AddMinutes(30)
            )
            .ToList();
        var outside = _model
            .SocialPosts.Where(post =>
                post.CreatedAt < peakTime || post.CreatedAt > peakTime.AddMinutes(30)
            )
            .ToList();
        if (afterPeak.Count == 0 || outside.Count == 0)
            return null;

        var windowAverage = afterPeak.Average(post => post.Engagement);
        var baselineAverage = outside.Average(post => post.Engagement);
        if (baselineAverage <= 0)
            return null;
        var change = (windowAverage - baselineAverage) / baselineAverage * 100;
        var direction = change >= 0 ? "plus" : "moins";
        return $"Dans les 30 minutes suivant le pic d'audience, les publications disponibles ont généré en moyenne {PdfTextSanitizer.FormatFrench(Math.Abs(change), "N1")} % {direction} d'interactions que les autres publications de la période.";
    }

    private void SectionTitle(IContainer container, string title, string eyebrow)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text(eyebrow).Bold().FontSize(8).FontColor(Secondary);
            column.Item().Text(title).Bold().FontSize(22).FontColor(Primary);
        });
    }

    private static void Subheading(IContainer container, string title) =>
        container.Text(Truncate(title, 100)).Bold().FontSize(12).FontColor("#334155");

    private void HeroCard(IContainer container, string label, string value, int fontSize = 27)
    {
        container
            .Background("#F8FAFC")
            .Border(1)
            .BorderColor("#E2E8F0")
            .BorderTop(3)
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(6);
                column.Item().Text(label).FontSize(8).FontColor("#64748B");
                column.Item().Text(value).Bold().FontSize(fontSize).FontColor(Primary);
            });
    }

    private static void InfoCard(IContainer container, string label, string value)
    {
        container
            .Background("#F8FAFC")
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(5);
                column.Item().Text(label).Bold().FontSize(8).FontColor("#64748B");
                column.Item().Text(value).FontSize(11).FontColor("#0F172A");
            });
    }

    private static void InsightCard(IContainer container, string value) =>
        container
            .Background("#F0FDFA")
            .BorderLeft(3)
            .BorderColor("#06B6D4")
            .Padding(12)
            .Text(Truncate(value, 800))
            .FontSize(11)
            .LineHeight(1.45f);

    private void RecommendationCard(IContainer container, int number, string content)
    {
        container
            .Background("#F8FAFC")
            .Border(1)
            .BorderColor("#E2E8F0")
            .Padding(12)
            .Row(row =>
            {
                row.ConstantItem(24)
                    .Height(24)
                    .Background(Secondary)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(number.ToString(CultureInfo.InvariantCulture))
                    .Bold()
                    .FontColor(Colors.White);
                row.RelativeItem()
                    .PaddingLeft(8)
                    .Text(Truncate(Clean(content), 380))
                    .FontSize(9)
                    .LineHeight(1.35f);
            });
    }

    private static void PlatformBadges(
        IContainer container,
        IEnumerable<string> platforms,
        int perRow = 5
    )
    {
        var items = platforms
            .Where(platform => !string.IsNullOrWhiteSpace(platform))
            .DistinctBy(PlatformIcons.Label)
            .ToList();
        if (items.Count == 0)
            return;

        // Row ne passe pas à la ligne : on découpe en lignes de perRow badges.
        container.Column(column =>
        {
            column.Spacing(6);
            foreach (var chunk in items.Chunk(perRow))
            {
                column
                    .Item()
                    .Row(row =>
                    {
                        row.Spacing(6);
                        foreach (var platform in chunk)
                            row.AutoItem().Element(item => PlatformBadge(item, platform));
                    });
            }
        });
    }

    private static void PlatformBadge(IContainer container, string platform)
    {
        var icon = PlatformIcons.Svg(platform, "#FFFFFF");
        container
            .Background(PlatformIcons.Color(platform))
            .PaddingHorizontal(8)
            .PaddingVertical(5)
            .Row(row =>
            {
                row.Spacing(5);
                if (icon is not null)
                    row.ConstantItem(10).Height(10).Svg(icon);
                row.AutoItem()
                    .Text(PlatformIcons.Label(platform))
                    .Bold()
                    .FontSize(8)
                    .FontColor(Colors.White);
            });
    }

    private static void PlatformTag(IContainer container, string platform, float size = 9)
    {
        var color = PlatformIcons.Color(platform);
        var icon = PlatformIcons.Svg(platform, color);
        container.Row(row =>
        {
            row.Spacing(4);
            if (icon is not null)
                row.ConstantItem(size + 1).Height(size + 1).Svg(icon);
            row.AutoItem()
                .Text(PlatformIcons.Label(platform))
                .Bold()
                .FontSize(size)
                .FontColor(color);
        });
    }

    private void PlatformsCard(IContainer container, string label)
    {
        var platforms = new List<string>();
        if (!string.IsNullOrWhiteSpace(_model.EventPlatform))
            platforms.Add(_model.EventPlatform);
        platforms.AddRange(_model.SocialPosts.Select(post => PlatformIcons.Label(post.Platform)));
        if (_model.TwitterAnalytics is not null)
            platforms.Add(PlatformIcons.Label(SocialPlatform.Twitter));

        container
            .MinHeight(80)
            .Background("#F8FAFC")
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(8);
                column.Item().Text(label).Bold().FontSize(8).FontColor("#64748B");
                column.Item().Element(item => PlatformBadges(item, platforms, 2));
            });
    }

    private static string Clean(string? value) => PdfTextSanitizer.Sanitize(value);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength].TrimEnd()}…";
}
