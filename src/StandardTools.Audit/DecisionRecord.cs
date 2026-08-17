using System.Text.Json;

namespace StandardTools.Audit;

/// <summary>
/// Captures a single tool decision with hash-chaining metadata.
/// </summary>
public sealed record DecisionRecord
{
    public required string RequestID { get; init; }
    public DateTime RecordedAt { get; init; }
    public required string ToolName { get; init; }

    /// <summary>
    /// Tool input. After writing, this is canonicalized to a <see cref="JsonElement"/>.
    /// </summary>
    public object? Input { get; init; }

    public required string InputHash { get; init; }

    /// <summary>
    /// Tool output. After writing, this is canonicalized to a <see cref="JsonElement"/>.
    /// </summary>
    public object? Output { get; init; }

    public required string OutputHash { get; init; }
    public required string Status { get; init; }
    public string? Error { get; init; }
    public string? GitCommitSHA { get; init; }
    public string? PackageVersion { get; init; }
    public long RandomSeed { get; init; }
    public string PrevRecordHash { get; init; } = string.Empty;
    public required string RecordHash { get; init; }
}
