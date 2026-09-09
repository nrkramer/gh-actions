using System.Text.Json.Serialization;

namespace GhActions.Core;

// The property names here are load-bearing: they are the on-disk cache
// format that every front end reads. Do not let a naming policy camelCase
// them.

public sealed class Job
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("platform")] public string Platform { get; set; } = "";
    [JsonPropertyName("short")] public string Short { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("conclusion")] public string? Conclusion { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("started")] public string Started { get; set; } = "";

    [JsonIgnore] public string State => Vocab.StateOf(Status, Conclusion);
}

public sealed class Run
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("number")] public long Number { get; set; }
    [JsonPropertyName("branch")] public string Branch { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("conclusion")] public string? Conclusion { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("started")] public string Started { get; set; } = "";
    [JsonPropertyName("event")] public string Event { get; set; } = "";
    [JsonPropertyName("jobs")] public List<Job> Jobs { get; set; } = new();
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public sealed class RepoResult
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("active")] public List<Run> Active { get; set; } = new();
    [JsonPropertyName("last")] public Run? Last { get; set; }
}

public sealed class Cache
{
    [JsonPropertyName("updated")] public double Updated { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("org")] public string Org { get; set; } = "";
    [JsonPropertyName("repos")] public List<RepoResult> Repos { get; set; } = new();
}

/// <summary>One ETag entry: the tag plus the distilled payload it stood for.</summary>
public sealed class StoreEntry
{
    [JsonPropertyName("etag")] public string? Etag { get; set; }
    [JsonPropertyName("data")] public System.Text.Json.JsonElement Data { get; set; }
}
