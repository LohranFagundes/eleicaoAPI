namespace ElectionApi.Net.Services;

public interface IDateTimeService
{
    DateTime Now { get; }
    DateTime UtcNow { get; }
    DateTime ToBrazilTime(DateTime utcDateTime);
    DateTime ToUtc(DateTime brazilDateTime);
    string FormatBrazilTime(DateTime dateTime);
}