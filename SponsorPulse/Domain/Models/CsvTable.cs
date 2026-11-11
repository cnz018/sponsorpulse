namespace SponsorPulse.Domain.Models;

public record CsvTable
{
    public List<string> Headers { get; init; } = new();
    public List<List<string>> Rows { get; init; } = new();
    public string Raw { get; init; } = string.Empty;
}
