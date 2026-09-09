using System.Text.Json;
using System.Text.Json.Serialization;

namespace GhActions.Core;

public sealed class Config
{
    [JsonPropertyName("org")] public string Org { get; set; } = "";
    [JsonPropertyName("repos")] public List<string> Repos { get; set; } = new();
    [JsonPropertyName("active_poll_seconds")] public int ActivePollSeconds { get; set; } = 15;
    [JsonPropertyName("idle_poll_seconds")] public int IdlePollSeconds { get; set; } = 60;
    [JsonPropertyName("runs_per_repo")] public int RunsPerRepo { get; set; } = 20;

    /// <summary>Optional. Overrides `gh auth token` when set; the Windows box
    /// may not have the gh CLI installed.</summary>
    [JsonPropertyName("token")] public string? Token { get; set; }

    private static readonly JsonSerializerOptions Opts = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Config Load()
    {
        var path = Paths.ConfigPath;
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"No config at {path}. Create it with an \"org\" and a \"repos\" list.", path);

        return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Opts)
               ?? throw new InvalidDataException($"{path} is not a JSON object");
    }

    /// <summary>Starter config written when the user picks "Edit config" and none exists.</summary>
    public static string Template => ConfigTemplateHolder.Text;

    public static Config LoadOrDefault()
    {
        try { return Load(); }
        catch { return new Config(); }
    }
}

public static class ConfigTemplateHolder
{
    public const string Text = """
    {
      "org": "YOUR-ORG-OR-USERNAME",
      "repos": [
        "first-repo",
        "second-repo"
      ],

      "_comment": "Add or remove repos above; the tray picks it up on the next poll, no restart needed.",

      "active_poll_seconds": 15,
      "idle_poll_seconds": 60,
      "runs_per_repo": 20,

      "_token": "Optional. Omit to use the gh CLI or the GH_TOKEN environment variable.",
      "token": ""
    }
    """;
}
