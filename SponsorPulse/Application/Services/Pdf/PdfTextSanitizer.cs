using System.Globalization;
using System.Text.RegularExpressions;

namespace SponsorPulse.Application.Services.Pdf;

public static partial class PdfTextSanitizer
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    [GeneratedRegex(
        @"\$\(\s*([\d\s\u00a0\u202f.,]+)\s*/\s*([\d\s\u00a0\u202f.,]+)\s*\)\s*(?:\\times|×)\s*([\d\s\u00a0\u202f.,]+)\s*=\s*([\d\s\u00a0\u202f.,]+)\s*\$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex CurrencyFormulaRegex();

    // Ignore @Twitter, #Twitter et twitter.com pour ne pas casser handles et liens.
    [GeneratedRegex(
        @"(?<![\w@#/.])Twitter\s*/\s*X(?!\w)|(?<![\w@#/.])X\s*/\s*Twitter(?!\w|\.\w)|(?<![\w@#/.])Twitter(?!\w|\.\w)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex TwitterNameRegex();

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = value
            .Replace("\\\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("$n^{\\circ}1$", "n°1", StringComparison.Ordinal)
            .Replace("n^{\\circ}1", "n°1", StringComparison.Ordinal)
            .Replace("\\times", "×", StringComparison.Ordinal)
            .Replace("\\cdot", "·", StringComparison.Ordinal)
            .Replace("\\%", "%", StringComparison.Ordinal);

        result = CurrencyFormulaRegex()
            .Replace(
                result,
                match =>
                {
                    var numerator = match.Groups[1].Value.Trim();
                    var denominator = match.Groups[2].Value.Trim();
                    var factorText = match.Groups[3].Value;
                    var resultText = match.Groups[4].Value;

                    if (
                        !TryParseNumber(factorText, out var factor)
                        || !TryParseNumber(resultText, out var amount)
                    )
                        return match.Value;

                    return $"({FormatFormulaNumber(numerator)} / {FormatFormulaNumber(denominator)}) × {FormatCurrency(factor)} = {FormatCurrency(amount)}";
                }
            );

        return TwitterNameRegex().Replace(result.Replace("$", string.Empty, StringComparison.Ordinal), "X");
    }

    public static string FormatNumber(long value) => FormatFrench(value, "N0");

    public static string FormatNumber(double value) => FormatFrench(value, "N0");

    public static string FormatPercent(double value) => $"{FormatFrench(value, "N2")} %";

    public static string FormatCurrency(decimal value) => $"{FormatFrench(value, "N2")} €";

    public static string FormatFrench(double value, string format) =>
        FormatFrench((IFormattable)value, format);

    public static string NormalizeColor(string? color, string fallback) =>
        color is { Length: 7 }
        && color[0] == '#'
        && color.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0
            ? color
            : fallback;

    public static string FormatDuration(TimeSpan value) =>
        $"{(long)value.TotalHours:00}h {value.Minutes:00}m";

    private static string FormatFrench<T>(T value, string format)
        where T : IFormattable =>
        value.ToString(format, FrenchCulture).Replace('\u202f', ' ').Replace('\u00a0', ' ');

    private static string FormatFrench(IFormattable value, string format) =>
        value.ToString(format, FrenchCulture).Replace('\u202f', ' ').Replace('\u00a0', ' ');

    private static string FormatFormulaNumber(string value) =>
        TryParseNumber(value, out var number) ? FormatFrench(number, "N0") : value.Trim();

    private static bool TryParseNumber(string value, out decimal number)
    {
        var normalized = value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00a0", string.Empty, StringComparison.Ordinal)
            .Replace("\u202f", string.Empty, StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal);

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out number
        );
    }
}
