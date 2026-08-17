using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandardTools.Metrics;

/// <summary>
/// Cumulative and annualized return statistics computed from a price series.
/// </summary>
[JsonConverter(typeof(ReturnMetricsJsonConverter))]
public readonly record struct ReturnMetrics
{
    public double CumulativeReturn { get; init; }
    public double Cagr { get; init; }
    public double AnnualizedVolatility { get; init; }
}

internal sealed class ReturnMetricsJsonConverter : JsonConverter<ReturnMetrics>
{
    public override ReturnMetrics Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of ReturnMetrics is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, ReturnMetrics value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("cumulative_return");
        WriteNumberOrNull(writer, value.CumulativeReturn);
        writer.WritePropertyName("cagr");
        WriteNumberOrNull(writer, value.Cagr);
        writer.WritePropertyName("annualized_volatility");
        WriteNumberOrNull(writer, value.AnnualizedVolatility);
        writer.WriteEndObject();
    }

    private static void WriteNumberOrNull(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value);
        }
    }
}
