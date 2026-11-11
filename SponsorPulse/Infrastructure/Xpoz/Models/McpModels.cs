using System.Text.Json.Serialization;

namespace SponsorPulse.Infrastructure.Xpoz.Models;

// Structure de la requête envoyée au serveur MCP
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;

    [JsonPropertyName("method")]
    public string Method { get; set; } = "tools/call";

    [JsonPropertyName("params")]
    public McpParams Params { get; set; } = new();
}

public class McpParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; set; } = new();
}

// Structure de la réponse reçue du serveur MCP
public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public McpResult? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

public class McpResult
{
    [JsonPropertyName("content")]
    public List<McpContent>? Content { get; set; }
}

public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
