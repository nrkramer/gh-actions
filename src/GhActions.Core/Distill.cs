using System.Text.Json;

namespace GhActions.Core;

/// <summary>
/// Shrink GitHub's responses to the handful of fields the UI shows. Two
/// reasons: the ETag store is written to disk every poll and full run payloads
/// are enormous, and it keeps every consumer reading one flat shape.
/// </summary>
public static class Distill
{
    private static string Str(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string? NullableStr(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static long Num(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64() : 0;

    private static string FirstNonEmpty(params string[] xs) =>
        xs.FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";

    private static IEnumerable<JsonElement> Items(JsonElement raw, string prop)
    {
        if (raw.ValueKind != JsonValueKind.Object) yield break;
        if (!raw.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
        foreach (var it in arr.EnumerateArray()) yield return it;
    }

    public static List<Run> Runs(JsonElement raw)
    {
        var outp = new List<Run>();
        foreach (var r in Items(raw, "workflow_runs"))
            outp.Add(new Run
            {
                Id = Num(r, "id"),
                Name = FirstNonEmpty(Str(r, "name"), Str(r, "display_title"), "workflow"),
                Number = Num(r, "run_number"),
                Branch = Str(r, "head_branch"),
                Status = Str(r, "status"),
                Conclusion = NullableStr(r, "conclusion"),
                Url = Str(r, "html_url"),
                Started = FirstNonEmpty(Str(r, "run_started_at"), Str(r, "created_at")),
                Event = Str(r, "event"),
            });
        return outp;
    }

    public static List<Job> Jobs(JsonElement raw)
    {
        var outp = new List<Job>();
        foreach (var j in Items(raw, "jobs"))
        {
            var name = FirstNonEmpty(Str(j, "name"), "job");
            outp.Add(new Job
            {
                Name = name,
                Platform = Format.PlatformOf(name),
                Short = Format.ShortJobName(name),
                Status = Str(j, "status"),
                Conclusion = NullableStr(j, "conclusion"),
                Url = Str(j, "html_url"),
                Started = Str(j, "started_at"),
            });
        }
        return outp;
    }
}
