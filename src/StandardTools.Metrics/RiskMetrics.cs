using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandardTools.Metrics;

/// <summary>
/// Risk and risk-adjusted performance statistics computed from a price series.
/// </summary>
[JsonConverter(typeof(RiskMetricsJsonConverter))]
public readonly record struct RiskMetrics
{
    public double SharpeRatio { get; init; }
    public double SortinoRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public double CalmarRatio { get; init; }
    public double VaR95 { get; init; }
    public double CVaR95 { get; init; }
    public double Volatility { get; init; }
}

internal sealed class RiskMetricsJsonConverter : JsonConverter<RiskMetrics>
{
    public override RiskMetrics Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of RiskMetrics is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, RiskMetrics value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("sharpe_ratio");
        WriteNumberOrNull(writer, value.SharpeRatio);
        writer.WritePropertyName("sortino_ratio");
        WriteNumberOrNull(writer, value.SortinoRatio);
        writer.WritePropertyName("max_drawdown");
        WriteNumberOrNull(writer, value.MaxDrawdown);
        writer.WritePropertyName("calmar_ratio");
        WriteNumberOrNull(writer, value.CalmarRatio);
        writer.WritePropertyName("var_95");
        WriteNumberOrNull(writer, value.VaR95);
        writer.WritePropertyName("cvar_95");
        WriteNumberOrNull(writer, value.CVaR95);
        writer.WritePropertyName("volatility");
        WriteNumberOrNull(writer, value.Volatility);
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
