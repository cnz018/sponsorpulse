namespace SponsorPulse.Application.Common.Config;

/// <summary>
/// Configuration pour le service de storytelling LLM
/// </summary>
public record LlmSettings
{
    public string Url { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
    public string CompactSystemPrompt { get; init; } = string.Empty;
    public int MinimumSlideCount { get; init; } = 5;
    public int MaximumSlideCount { get; init; } = 6;
    public int TimeoutSeconds { get; init; } = 120;
    public int MaxTokens { get; init; } = 4000;
    public double Temperature { get; init; } = 0.2;
    public bool UseJsonResponseFormat { get; init; } = true;
    public string ReasoningEffort { get; init; } = "medium";
    public int MaxSocialPosts { get; init; } = 20;
    public int MaxTwitchSnapshots { get; init; } = 30;
}
