using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElectionApi.Net.Converters;

public class BrazilDateTimeConverter : JsonConverter<DateTime>
{
    private readonly TimeZoneInfo _brazilTimeZone;

    public BrazilDateTimeConverter()
    {
        _brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    }

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateTimeString = reader.GetString();
        if (DateTime.TryParse(dateTimeString, out var dateTime))
        {
            // If the incoming date doesn't specify timezone, assume it's Brazil time
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
            }
            return dateTime;
        }
        
        throw new JsonException($"Unable to parse DateTime: {dateTimeString}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        DateTime brazilTime;
        
        if (value.Kind == DateTimeKind.Utc)
        {
            brazilTime = TimeZoneInfo.ConvertTimeFromUtc(value, _brazilTimeZone);
        }
        else if (value.Kind == DateTimeKind.Unspecified)
        {
            // Treat unspecified as already in Brazil time
            brazilTime = value;
        }
        else
        {
            brazilTime = value;
        }

        // Format with timezone offset
        var offset = _brazilTimeZone.GetUtcOffset(brazilTime);
        var formattedDate = brazilTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + 
                           (offset.TotalHours >= 0 ? "+" : "") + 
                           offset.ToString(@"hh\:mm");
        
        writer.WriteStringValue(formattedDate);
    }
}

public class BrazilNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private readonly BrazilDateTimeConverter _dateTimeConverter;

    public BrazilNullableDateTimeConverter()
    {
        _dateTimeConverter = new BrazilDateTimeConverter();
    }

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return _dateTimeConverter.Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            _dateTimeConverter.Write(writer, value.Value, options);
        }
    }
}