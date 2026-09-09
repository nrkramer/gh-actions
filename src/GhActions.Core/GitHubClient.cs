using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GhActions.Core;

/// <summary>Conditional GETs against the Actions API.</summary>
public sealed class GitHubClient
{
    public const string Api = "https://api.github.com";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    // One client for the process. The tray polls every 15 seconds for days, and
    // a client per poll would leave a connection pool and sockets in TIME_WAIT
    // each time. Because it is shared, the token goes on the request rather
    // than on DefaultRequestHeaders -- it can change between polls.
    private static readonly HttpClient Http = new() { Timeout = Timeout };

    private readonly string _token;
    private readonly Dictionary<string, StoreEntry> _store;
    private readonly object _storeLock = new();

    public GitHubClient(string token, Dictionary<string, StoreEntry> store)
    {
        _token = token;
        _store = store;
    }

    /// <summary>
    /// GET with an ETag, returning the distilled payload. A 304 replays what
    /// was stored, and crucially does not count against the 5000/hour limit --
    /// which is what makes a 15 second poll across several repos affordable.
    /// </summary>
    public async Task<T> FetchAsync<T>(string url, Func<JsonElement, T> distill, CancellationToken ct)
        where T : class
    {
        StoreEntry? entry;
        lock (_storeLock) _store.TryGetValue(url, out entry);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        req.Headers.UserAgent.ParseAdd("gh-actions-core");
        if (!string.IsNullOrEmpty(entry?.Etag))
            req.Headers.TryAddWithoutValidation("If-None-Match", entry.Etag);

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.NotModified)
        {
            if (entry is not null && entry.Data.ValueKind != JsonValueKind.Undefined)
            {
                var cached = entry.Data.Deserialize<T>(JsonFile.Opts);
                if (cached is not null) return cached;
            }
            // 304 with nothing stored: the store was cleared behind our back.
            // Drop the tag so the next poll refetches in full.
            lock (_storeLock) _store.Remove(url);
            return distill(default);
        }

        if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            && resp.Headers.TryGetValues("X-RateLimit-Remaining", out var rem)
            && rem.FirstOrDefault() == "0")
            throw new InvalidOperationException("rate limited");

        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var data = distill(doc.RootElement);

        lock (_storeLock)
            _store[url] = new StoreEntry
            {
                Etag = resp.Headers.ETag?.ToString(),
                Data = JsonSerializer.SerializeToElement(data, JsonFile.Opts),
            };

        return data;
    }
}
