using System.Text.Json;

namespace GhActions.Core;

public static class JsonFile
{
    public static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public static T Read<T>(string path, T fallback)
    {
        try
        {
            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(text, Opts) ?? fallback;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    /// <summary>Write via a temp file and rename, so a reader never sees a
    /// half-written cache. The panel polls this file on a timer.</summary>
    public static void WriteAtomic<T>(string path, T data)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Opts));
        File.Move(tmp, path, overwrite: true);
    }
}
