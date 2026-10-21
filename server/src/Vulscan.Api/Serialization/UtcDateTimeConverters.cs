using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulscan.Api.Serialization;

/// <summary>
/// Every timestamp in Vulscan is UTC, but EF Core materialises them as <see cref="DateTimeKind.Unspecified"/>, which
/// System.Text.Json writes with no offset ("2026-10-20T18:53:49"). Browsers parse an offset-less ISO string as LOCAL
/// time, so the UI would show every time shifted by the viewer's UTC offset. These converters always emit UTC with a
/// trailing "Z", and treat offset-less input as UTC.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ToUtc(DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value));

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : new UtcDateTimeConverter().Read(ref reader, typeof(DateTime), options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(UtcDateTimeConverter.ToUtc(value.Value));
    }
}
