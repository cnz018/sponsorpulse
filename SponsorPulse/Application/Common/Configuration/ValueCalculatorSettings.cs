namespace SponsorPulse.Application.Common.Configuration;

/// <summary>
/// Configuration du calculateur de valeur sponsor.
/// Toutes les valeurs sont chargées depuis appsettings.json.
/// </summary>
public class ValueCalculatorSettings
{
    public DefaultValuesSettings DefaultValues { get; set; } = new();
    public MinValuesSettings MinValues { get; set; } = new();
    public MaxValuesSettings MaxValues { get; set; } = new();
    public StepValuesSettings StepValues { get; set; } = new();
    public CoefficientsSettings Coefficients { get; set; } = new();
}

public class DefaultValuesSettings
{
    public int AMA { get; set; } = 2000;
    public int StreamHours { get; set; } = 100;
    public double EngagementRate { get; set; } = 3.5;
    public int SocialReach { get; set; } = 45000;
    public bool IsExclusive { get; set; } = false;
}

public class MinValuesSettings
{
    public int AMA { get; set; } = 10;
    public int StreamHours { get; set; } = 1;
    public double EngagementRate { get; set; } = 0.1;
    public int SocialReach { get; set; } = 100;
}

public class MaxValuesSettings
{
    public int AMA { get; set; } = 50000;
    public int StreamHours { get; set; } = 500;
    public double EngagementRate { get; set; } = 20;
    public int SocialReach { get; set; } = 500000;
}

public class StepValuesSettings
{
    public int AMA { get; set; } = 50;
    public int StreamHours { get; set; } = 5;
    public double EngagementRate { get; set; } = 0.1;
    public int SocialReach { get; set; } = 500;
}

public class CoefficientsSettings
{
    public double AMA { get; set; } = 0.03;
    public double EngagementRate { get; set; } = 120;
    public double SocialReach { get; set; } = 0.05;
    public double ExclusiveBonus { get; set; } = 0.20;
}
