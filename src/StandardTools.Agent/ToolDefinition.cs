using System.Text.Json;

namespace StandardTools.Agent;

/// <summary>
/// Describes an agent-callable tool.
/// </summary>
public sealed record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonElement Parameters { get; init; }
}
