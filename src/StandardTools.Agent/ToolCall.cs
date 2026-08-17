using System.Text.Json;

namespace StandardTools.Agent;

/// <summary>
/// A request from an agent to invoke a tool.
/// </summary>
public sealed record ToolCall
{
    public required string Name { get; init; }
    public required JsonElement Arguments { get; init; }
}
