using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SponsorPulse.Domain.Enums;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Application.Services.Pdf;

public sealed class QuestPdfReportDocument(
    ReportModel model,
    IReadOnlyDictionary<string, byte[]> images
) : IDocument
{
    private readonly ReportModel _model = model;
    private readonly IReadOnlyDictionary<string, byte[]> _images = images;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        ComposePage(container, ComposePresentation);

        var slides = _model
            .StorytellingSlides.Where(slide => slide.Type != StorytellingSlideType.Unknown)
            .ToList();

        if (slides.Count == 0)
            return;

        foreach (var slideType in GetRenderOrder())
        {
            var typedSlides = slides.Where(slide => slide.Type == slideType).ToList();
            if (typedSlides.Count == 0)
                continue;

            if (
                slideType
                is StorytellingSlideType.StreamMetrics
                    or StorytellingSlideType.SocialNetworkMetrics
            )
            {
                ComposePage(container, content => ComposeGroupedSlides(content, typedSlides));
                continue;
            }

            foreach (var slide in typedSlides)
            {
                ComposePage(container, content => ComposeSlide(content, slide));
            }
        }
    }

    private void ComposePresentation(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(18);
            column
                .Item()
                .Background(ParseColor(_model.PrimaryColor))
                .Padding(28)
                .Column(hero =>
                {
                    hero.Spacing(10);
                    hero.Item().Text("SPONSORPULSE").Bold().FontSize(12).FontColor(Colors.White);
                    hero.Item()
                        .Text("Rapport d'Impact Sponsoring")
                        .Bold()
                        .FontSize(26)
                        .FontColor(Colors.White);
                    hero.Item().Text(_model.EventName).FontSize(18).FontColor(Colors.White);
                });

            column
                .Item()
                .Column(details =>
                {
                    details.Spacing(6);
                    details
                        .Item()
                        .Text("Présentation de l'événement")
                        .Bold()
                        .FontSize(18)
                        .FontColor(ParseColor(_model.PrimaryColor));
                    if (!string.IsNullOrWhiteSpace(_model.EventDescription))
                        details.Item().Text(NormalizeContent(_model.EventDescription));
                    details
                        .Item()
                        .Text(text =>
                        {
                            text.Span("Date : ").Bold();
                            text.Span(_model.EventDate.ToString("dd/MM/yyyy"));
                            text.Span("    Plateforme : ").Bold();
                            text.Span(_model.EventPlatform);
                        });
                });

            var cards = new List<(string Label, string Value)>();
            if (_model.HasViewerCount && _model.TwitchMetrics is not null)
                cards.Add(("Viewers", _model.TwitchMetrics.ViewerCount.ToString("N0")));
            if (_model.HasPeakViewers && _model.TwitchMetrics is not null)
                cards.Add(("Pic viewers", _model.TwitchMetrics.PeakViewers.ToString("N0")));
            if (_model.HasStreamDuration && _model.TwitchMetrics is not null)
                cards.Add(("Durée", _model.TwitchMetrics.StreamDuration.ToString(@"hh\:mm")));

            if (_model.TwitterAnalytics is not null)
            {
                cards.Add(
                    ("Impressions", _model.TwitterAnalytics.EstimatedImpressions.ToString("N0"))
                );
                cards.Add(("Engagement", _model.TwitterAnalytics.EngagementRate.ToString("P1")));
                cards.Add(("Tweets", _model.TwitterAnalytics.TotalTweets.ToString("N0")));
            }

            if (cards.Count > 0)
            {
                column
                    .Item()
                    .Text("Métriques disponibles")
                    .Bold()
                    .FontSize(16)
                    .FontColor(ParseColor(_model.SecondaryColor));
                for (var index = 0; index < cards.Count; index += 3)
                {
                    var rowCards = cards.Skip(index).Take(3).ToList();
                    column
                        .Item()
                        .Row(row =>
                        {
                            row.Spacing(10);
                            foreach (var card in rowCards)
                                row.RelativeItem()
                                    .Element(content =>
                                        AddMetricCard(content, card.Label, card.Value)
                                    );
                            for (var filler = rowCards.Count; filler < 3; filler++)
                                row.RelativeItem();
                        });
                }
            }
        });
    }

    private void ComposePage(IDocumentContainer container, Action<IContainer> compose)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(10));
            page.Header().Element(ComposeHeader);
            compose(page.Content());
            page.Footer()
                .AlignCenter()
                .Text(text =>
                {
                    text.Span("Propulsé par SponsorPulse - ");
                    text.CurrentPageNumber();
                });
        });
    }

    private static IReadOnlyList<StorytellingSlideType> GetRenderOrder() =>
        new[]
        {
            StorytellingSlideType.ExecutiveSummary,
            StorytellingSlideType.StreamMetrics,
            StorytellingSlideType.SocialNetworkMetrics,
            StorytellingSlideType.TwitchTwitterSynergy,
            StorytellingSlideType.SponsorValue,
            StorytellingSlideType.EventSummary,
            StorytellingSlideType.Recommendations,
        };

    private void ComposeGroupedSlides(
        IContainer container,
        IReadOnlyList<StorytellingReponse.Slide> slides
    )
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column
                .Item()
                .Text(GetGroupTitle(slides[0].Type))
                .Bold()
                .FontSize(20)
                .FontColor(ParseColor(_model.PrimaryColor));
            for (var index = 0; index < slides.Count; index += 2)
            {
                var rowSlides = slides.Skip(index).Take(2).ToList();
                column
                    .Item()
                    .Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem()
                            .Element(content => ComposeSlideBlock(content, rowSlides[0]));

                        if (rowSlides.Count == 2)
                            row.RelativeItem()
                                .Element(content => ComposeSlideBlock(content, rowSlides[1]));
                    });
            }
        });
    }

    private void ComposeSlide(IContainer container, StorytellingReponse.Slide slide)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column
                .Item()
                .Text(slide.Titre)
                .Bold()
                .FontSize(20)
                .FontColor(ParseColor(_model.PrimaryColor));
            column.Item().Element(content => ComposeSlideBlock(content, slide));
        });
    }

    private void ComposeSlideBlock(IContainer container, StorytellingReponse.Slide slide)
    {
        container
            .Background(Colors.Grey.Lighten4)
            .Padding(14)
            .Column(column =>
            {
                column.Spacing(8);
                if (!string.IsNullOrWhiteSpace(slide.Titre))
                    column
                        .Item()
                        .Text(slide.Titre)
                        .Bold()
                        .FontSize(13)
                        .FontColor(ParseColor(_model.SecondaryColor));

                if (!string.IsNullOrWhiteSpace(slide.Contenu))
                    column.Item().Text(NormalizeContent(slide.Contenu));

                if (slide.EstVisualisation && slide.DonneesVisualisation is not null)
                {
                    column
                        .Item()
                        .Background(Colors.White)
                        .Padding(8)
                        .Text(JsonSerializer.Serialize(slide.DonneesVisualisation))
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                }

                if (
                    !string.IsNullOrWhiteSpace(slide.ImageUrl)
                    && _images.TryGetValue(slide.ImageUrl, out var imageBytes)
                )
                {
                    column.Item().MaxHeight(220).Image(imageBytes).FitArea();
                }
            });
    }

    private static string GetGroupTitle(StorytellingSlideType type) =>
        type switch
        {
            StorytellingSlideType.StreamMetrics => "Métriques stream",
            StorytellingSlideType.SocialNetworkMetrics => "Métriques réseaux sociaux",
            _ => type.ToString(),
        };

    private static string NormalizeContent(string content) => content.Replace("\\n", "\n");

    private void ComposeHeader(IContainer container)
    {
        container
            .BorderBottom(1)
            .BorderColor(ParseColor(_model.PrimaryColor))
            .PaddingBottom(8)
            .Text(_model.CompanyName)
            .FontSize(9)
            .FontColor(Colors.Grey.Darken2);
    }

    private void ComposeCover(IContainer container)
    {
        container
            .Background(ParseColor(_model.PrimaryColor))
            .Padding(28)
            .Column(column =>
            {
                column.Spacing(12);
                column.Item().Text("SPONSORPULSE").Bold().FontSize(12).FontColor(Colors.White);
                column
                    .Item()
                    .Text("Rapport d'Impact Sponsoring")
                    .Bold()
                    .FontSize(28)
                    .FontColor(Colors.White);
                column.Item().Text(_model.EventName).FontSize(18).FontColor(Colors.White);
                column
                    .Item()
                    .Text($"Préparé pour {_model.CompanyName}")
                    .FontSize(12)
                    .FontColor(Colors.White);
                column.Item().Text(_model.EventDate.ToString("dd/MM/yyyy")).FontColor(Colors.White);
            });
    }

    private void ComposeOverview(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column
                .Item()
                .Text("Synthèse de l'événement")
                .Bold()
                .FontSize(18)
                .FontColor(ParseColor(_model.PrimaryColor));
            column
                .Item()
                .Text(text =>
                {
                    text.Span("Plateforme : ").Bold();
                    text.Span(_model.EventPlatform);
                    text.Span("   Date : ").Bold();
                    text.Span(_model.EventDate.ToString("dd/MM/yyyy"));
                });
            column.Item().Text(_model.AnalysisText);
        });
    }

    private void ComposeMetrics(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column
                .Item()
                .Text("Performance")
                .Bold()
                .FontSize(18)
                .FontColor(ParseColor(_model.SecondaryColor));
            column
                .Item()
                .Row(row =>
                {
                    row.Spacing(8);
                    AddMetric(
                        row.RelativeItem(),
                        "Viewers",
                        _model.TwitchMetrics?.ViewerCount.ToString("N0") ?? "N/A"
                    );
                    AddMetric(
                        row.RelativeItem(),
                        "Pic viewers",
                        _model.TwitchMetrics?.PeakViewers.ToString("N0") ?? "N/A"
                    );
                    AddMetric(
                        row.RelativeItem(),
                        "Durée",
                        _model.TwitchMetrics?.StreamDuration.ToString(@"hh\:mm") ?? "N/A"
                    );
                });

            if (_model.TwitterAnalytics is not null)
            {
                column
                    .Item()
                    .Row(row =>
                    {
                        row.Spacing(8);
                        AddMetric(
                            row.RelativeItem(),
                            "Impressions",
                            _model.TwitterAnalytics.EstimatedImpressions.ToString("N0")
                        );
                        AddMetric(
                            row.RelativeItem(),
                            "Engagement",
                            _model.TwitterAnalytics.EngagementRate.ToString("P1")
                        );
                        AddMetric(
                            row.RelativeItem(),
                            "Tweets",
                            _model.TwitterAnalytics.TotalTweets.ToString("N0")
                        );
                    });
            }
        });
    }

    private void ComposeStorytelling(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column
                .Item()
                .Text("Storytelling")
                .Bold()
                .FontSize(20)
                .FontColor(ParseColor(_model.PrimaryColor));

            if (_model.StorytellingSlides.Count == 0)
            {
                column.Item().Text("Aucune analyse storytelling disponible.").Italic();
                return;
            }

            foreach (var slide in _model.StorytellingSlides)
            {
                column
                    .Item()
                    .Background(Colors.Grey.Lighten4)
                    .Padding(12)
                    .Column(slideColumn =>
                    {
                        slideColumn.Spacing(6);
                        slideColumn
                            .Item()
                            .Text(slide.Titre)
                            .Bold()
                            .FontSize(13)
                            .FontColor(ParseColor(_model.SecondaryColor));
                        slideColumn.Item().Text(slide.Contenu);

                        if (slide.EstVisualisation && slide.DonneesVisualisation is not null)
                        {
                            slideColumn
                                .Item()
                                .Background(Colors.White)
                                .Padding(8)
                                .Text(JsonSerializer.Serialize(slide.DonneesVisualisation))
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken2);
                        }

                        if (
                            !string.IsNullOrWhiteSpace(slide.ImageUrl)
                            && _images.TryGetValue(slide.ImageUrl, out var imageBytes)
                        )
                        {
                            slideColumn.Item().Image(imageBytes).FitArea();
                        }
                    });
            }
        });
    }

    private void ComposeRecommendations(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column
                .Item()
                .Text("Recommandations")
                .Bold()
                .FontSize(18)
                .FontColor(ParseColor(_model.SecondaryColor));

            foreach (var suggestion in _model.Suggestions)
                column.Item().Text($"• {suggestion}");
        });
    }

    private static void AddMetric(IContainer container, string label, string value)
    {
        AddMetricCard(container, label, value);
    }

    private static void AddMetricCard(IContainer container, string label, string value)
    {
        container
            .Background(Colors.Grey.Lighten4)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
                column.Item().Text(value).Bold().FontSize(16);
            });
    }

    private static string ParseColor(string color)
    {
        return string.IsNullOrWhiteSpace(color) ? Colors.Blue.Medium : color;
    }
}
