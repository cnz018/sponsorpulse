namespace SponsorPulse.Domain.Primitives;

public static class DateHelper
{
    public static DateTime ToUtc(
        DateTime value,
        DateTimeKind unspecifiedKind = DateTimeKind.Local
    ) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, unspecifiedKind).ToUniversalTime(),
        };

    public static DateTime ToLocal(DateTime utcValue) =>
        DateTime.SpecifyKind(utcValue, DateTimeKind.Utc).ToLocalTime();

    public static DateTimeOffset ToUtc(DateTimeOffset value) => value.ToUniversalTime();

    public static DateTimeOffset ToLocal(DateTimeOffset value) => value.ToLocalTime();
}
