namespace ElectionApi.Net.Services;

public class DateTimeService : IDateTimeService
{
    private readonly TimeZoneInfo _brazilTimeZone;

    public DateTimeService()
    {
        // UTC-3 (Brasília/São Paulo timezone)
        _brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _brazilTimeZone);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime ToBrazilTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _brazilTimeZone);
        }
        
        // If it's already in Brazil time or unspecified, return as is
        return utcDateTime;
    }

    public DateTime ToUtc(DateTime brazilDateTime)
    {
        if (brazilDateTime.Kind == DateTimeKind.Utc)
        {
            return brazilDateTime;
        }

        // Convert Brazil time to UTC
        return TimeZoneInfo.ConvertTimeToUtc(brazilDateTime, _brazilTimeZone);
    }

    public string FormatBrazilTime(DateTime dateTime)
    {
        var brazilTime = ToBrazilTime(dateTime);
        return brazilTime.ToString("yyyy-MM-dd HH:mm:ss") + " BRT";
    }
}