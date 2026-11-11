using Microsoft.Extensions.Options;
using SponsorPulse.Application.Common.Configuration;

namespace SponsorPulse.Application.Services;

public interface IValueCalculatorService
{
    double CalculateSponsorValue(int ama, int streamHours, double engagementRate, int socialReach, bool isExclusive);
    ValueCalculatorBreakdown GetBreakdown(int ama, int streamHours, double engagementRate, int socialReach, bool isExclusive);
    ValueCalculatorSettings GetSettings();
}

public class ValueCalculatorService : IValueCalculatorService
{
    private readonly ValueCalculatorSettings _settings;

    public ValueCalculatorService(IOptions<ValueCalculatorSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Calcule la valeur sponsor selon la formule Market Standard 2026 :
    /// ValeurSponsor = (AMA × Hours × 0.03) + (ER × 120) + (SocialReach × 0.05) + Kexclu
    /// </summary>
    public double CalculateSponsorValue(int ama, int streamHours, double engagementRate, int socialReach, bool isExclusive)
    {
        var breakdown = GetBreakdown(ama, streamHours, engagementRate, socialReach, isExclusive);
        return breakdown.Total;
    }

    /// <summary>
    /// Retourne le détail du calcul pour affichage.
    /// </summary>
    public ValueCalculatorBreakdown GetBreakdown(int ama, int streamHours, double engagementRate, int socialReach, bool isExclusive)
    {
        // AMA Revenue: AMA × Hours × Coefficient AMA
        var amaRevenue = ama * streamHours * _settings.Coefficients.AMA;

        // Engagement Value: ER × Coefficient ER
        var engagementValue = engagementRate * _settings.Coefficients.EngagementRate;

        // Social Reach: Reach × Coefficient SocialReach
        var socialReachValue = socialReach * _settings.Coefficients.SocialReach;

        // Sous-total
        var subtotal = amaRevenue + engagementValue + socialReachValue;

        // Kexclu (Bonus exclusivité): Pourcentage du sous-total
        var exclusiveBonus = isExclusive ? subtotal * _settings.Coefficients.ExclusiveBonus : 0;

        // Total
        var total = subtotal + exclusiveBonus;

        return new ValueCalculatorBreakdown
        {
            AmaRevenue = amaRevenue,
            EngagementValue = engagementValue,
            SocialReachValue = socialReachValue,
            ExclusiveBonus = exclusiveBonus,
            Total = total,
            Coefficients = _settings.Coefficients
        };
    }

    public ValueCalculatorSettings GetSettings()
    {
        return _settings;
    }
}

/// <summary>
/// Détail du calcul de valeur sponsor.
/// </summary>
public record ValueCalculatorBreakdown
{
    public double AmaRevenue { get; init; }
    public double EngagementValue { get; init; }
    public double SocialReachValue { get; init; }
    public double ExclusiveBonus { get; init; }
    public double Total { get; init; }
    public CoefficientsSettings Coefficients { get; init; } = new();
}
