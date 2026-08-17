using System.Text.Json;

namespace StandardTools.Agent;

/// <summary>
/// The outcome of an agent tool invocation.
/// </summary>
public sealed record ToolResult
{
    public required JsonElement Output { get; init; }
    public string? Error { get; init; }

    public static ToolResult Ok(object output) =>
        new() { Output = JsonSerializer.SerializeToElement(output, AgentJsonOptions.Instance) };

    public static ToolResult ErrorResult(string error) =>
        new() { Output = JsonSerializer.SerializeToElement(new { error }, AgentJsonOptions.Instance), Error = error };
}
