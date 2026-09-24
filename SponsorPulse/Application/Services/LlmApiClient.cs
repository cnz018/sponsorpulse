using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SponsorPulse.Application.Common.Config;
using SponsorPulse.Application.Common.Interfaces;
using SponsorPulse.Application.Common.Models;
using SponsorPulse.Domain.Models;

namespace SponsorPulse.Application.Services;

public sealed class LlmApiClient(HttpClient httpClient, LlmSettings llmSettings) : ILlmApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiCallResponse<StorytellingReponse>> GenerateAsync(
        EventRawData data,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(llmSettings.Url))
        {
            return ApiCallResponse<StorytellingReponse>.Failed(
                HttpStatusCode.BadRequest,
                "L'URL du service LLM n'est pas configurée."
            );
        }

        if (string.IsNullOrWhiteSpace(llmSettings.Model))
        {
            return ApiCallResponse<StorytellingReponse>.Failed(
                HttpStatusCode.BadRequest,
                "Le modèle LLM n'est pas configuré."
            );
        }

        if (
            string.IsNullOrWhiteSpace(llmSettings.SystemPrompt)
            && string.IsNullOrWhiteSpace(llmSettings.CompactSystemPrompt)
        )
        {
            return ApiCallResponse<StorytellingReponse>.Failed(
                HttpStatusCode.BadRequest,
                "Le prompt système du storytelling n'est pas configuré."
            );
        }

        var requestPayload = new
        {
            model = llmSettings.Model,
            instructions = string.IsNullOrWhiteSpace(llmSettings.CompactSystemPrompt)
                ? llmSettings.SystemPrompt
                : llmSettings.CompactSystemPrompt,
            input = JsonSerializer.Serialize(BuildPromptPayload(data), JsonOptions),
            max_output_tokens = llmSettings.MaxTokens,
            reasoning = new { effort = llmSettings.ReasoningEffort },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, llmSettings.Url)
        {
            Content = JsonContent.Create(requestPayload, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(llmSettings.ApiKey))
            request.Headers.Authorization = new("Bearer", llmSettings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResponse<StorytellingReponse>.Failed(
                HttpStatusCode.RequestTimeout,
                "Le délai d'attente du service LLM a été dépassé."
            );
        }
        catch (HttpRequestException exception)
        {
            return ApiCallResponse<StorytellingReponse>.Failed(
                HttpStatusCode.BadGateway,
                $"Le service LLM est inaccessible : {exception.Message}"
            );
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResponse<StorytellingReponse>.Failed(
                    response.StatusCode,
                    responseBody
                );
            }

            var content = ExtractAssistantContent(responseBody);
            if (string.IsNullOrWhiteSpace(content))
            {
                return ApiCallResponse<StorytellingReponse>.Failed(
                    response.StatusCode,
                    "La réponse du service LLM ne contient aucun contenu exploitable."
                );
            }

            StorytellingReponse? storytellingResponse;
            try
            {
                storytellingResponse = JsonSerializer.Deserialize<StorytellingReponse>(
                    RemoveMarkdownFence(content),
                    JsonOptions
                );
            }
            catch (JsonException)
            {
                return ApiCallResponse<StorytellingReponse>.Failed(
                    response.StatusCode,
                    "La réponse du service LLM n'est pas un JSON valide."
                );
            }

            return ApiCallResponse<StorytellingReponse>.Succeeded(
                response.StatusCode,
                storytellingResponse!
            );
        }
    }

    private object BuildPromptPayload(EventRawData data)
    {
        var socialPosts = data
            .SocialPosts.OrderByDescending(post =>
                post.LikesCount + post.SharesCount + post.CommentsCount
            )
            .ThenByDescending(post => post.CreatedAt)
            .Take(Math.Max(0, llmSettings.MaxSocialPosts))
            .Select(post => new
            {
                post.AuthorName,
                post.AuthorHandle,
                post.AuthorFollowersCount,
                post.ImpressionsCount,
                post.ContentText,
                post.CreatedAt,
                post.Url,
                post.LikesCount,
                post.SharesCount,
                post.CommentsCount,
            })
            .ToList();

        var twitchSnapshots = data
            .TwitchSnapshots.OrderByDescending(snapshot => snapshot.ViewerCount)
            .ThenByDescending(snapshot => snapshot.ChatMessageCount)
            .Take(Math.Max(0, llmSettings.MaxTwitchSnapshots))
            .OrderBy(snapshot => snapshot.CapturedAt)
            .Select(snapshot => new
            {
                snapshot.ViewerCount,
                snapshot.PeakViewerCount,
                snapshot.GameName,
                snapshot.ChatMessageCount,
                snapshot.CapturedAt,
            })
            .ToList();

        var analytics = data.Event.TwitterAnalytics;

        return new
        {
            Event = new
            {
                data.Event.Name,
                data.Event.Description,
                data.Event.Date,
                data.Event.StreamPlatform,
                data.Event.ViewerCount,
                data.Event.PeakViewers,
                data.Event.StreamDuration,
                data.Event.StartedAt,
                data.Event.GameName,
                data.Event.TwitterHashtags,
                TwitterAnalytics = analytics is null
                    ? null
                    : new
                    {
                        analytics.TotalTweets,
                        analytics.TotalEngagement,
                        analytics.EstimatedImpressions,
                        analytics.UniqueReach,
                        analytics.AdValueEquivalent,
                        analytics.EngagementRate,
                        analytics.SentimentScore,
                        analytics.ViralMultiplier,
                        analytics.ShareOfVoice,
                        analytics.EstimatedROI,
                        analytics.CPM,
                        TopInfluencer = new
                        {
                            analytics.TopInfluencer.Name,
                            analytics.TopInfluencer.Handle,
                            analytics.TopInfluencer.FollowersCount,
                            analytics.TopInfluencer.TweetCount,
                            analytics.TopInfluencer.AvgEngagementRate,
                            analytics.TopInfluencer.EstimatedReach,
                        },
                        TopTweets = analytics
                            .TopTweets.Take(5)
                            .Select(tweet => new
                            {
                                tweet.AuthorName,
                                tweet.AuthorHandle,
                                tweet.AuthorFollowersCount,
                                tweet.Text,
                                tweet.Url,
                                tweet.Likes,
                                tweet.Retweets,
                                tweet.Replies,
                                tweet.CreatedAt,
                                tweet.Impressions,
                                tweet.EngagementRate,
                            }),
                        TopHashtags = analytics
                            .TopHashtags.Take(10)
                            .Select(hashtag => new
                            {
                                hashtag.Tag,
                                hashtag.UsageCount,
                                hashtag.TotalEngagements,
                                hashtag.ReachEstimate,
                            }),
                        analytics.SentimentRatio,
                    },
            },
            Media = data.Media.Select(media => new { media.OriginalFileName, media.Url }).ToList(),
            SocialPosts = socialPosts,
            TwitchSnapshots = twitchSnapshots,
        };
    }

    private static string? ExtractAssistantContent(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (
                document.RootElement.TryGetProperty("output", out var output)
                && output.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var item in output.EnumerateArray())
                {
                    if (
                        !item.TryGetProperty("content", out var content)
                        || content.ValueKind != JsonValueKind.Array
                    )
                        continue;

                    foreach (var contentItem in content.EnumerateArray())
                    {
                        if (
                            contentItem.TryGetProperty("text", out var text)
                            && text.ValueKind == JsonValueKind.String
                        )
                            return text.GetString();
                    }
                }
            }

            return responseBody;
        }
        catch (JsonException)
        {
            return responseBody;
        }
    }

    private static string RemoveMarkdownFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstLineEnd = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLineEnd < 0 || lastFence <= firstLineEnd)
            return trimmed;

        return trimmed[(firstLineEnd + 1)..lastFence].Trim();
    }
}
