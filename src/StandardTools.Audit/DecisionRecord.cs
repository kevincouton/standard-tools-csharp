using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandardTools.Audit;

/// <summary>
/// Captures a single tool decision with hash-chaining metadata.
/// </summary>
public sealed record DecisionRecord
{
    [JsonPropertyName("request_id")]
    public required string RequestID { get; init; }

    [JsonPropertyName("recorded_at")]
    public DateTime RecordedAt { get; init; }

    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Tool input. After writing, this is canonicalized to a <see cref="JsonElement"/>.
    /// </summary>
    [JsonPropertyName("input")]
    public object? Input { get; init; }

    [JsonPropertyName("input_hash")]
    public required string InputHash { get; init; }

    /// <summary>
    /// Tool output. After writing, this is canonicalized to a <see cref="JsonElement"/>.
    /// </summary>
    [JsonPropertyName("output")]
    public object? Output { get; init; }

    [JsonPropertyName("output_hash")]
    public required string OutputHash { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("git_commit_sha")]
    public string? GitCommitSHA { get; init; }

    [JsonPropertyName("package_version")]
    public string? PackageVersion { get; init; }

    [JsonPropertyName("random_seed")]
    public long RandomSeed { get; init; }

    [JsonPropertyName("prev_record_hash")]
    public string PrevRecordHash { get; init; } = string.Empty;

    [JsonPropertyName("record_hash")]
    public required string RecordHash { get; init; }
}
