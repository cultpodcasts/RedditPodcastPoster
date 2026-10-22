using System.Text;

namespace RedditPodcastPoster.ContentPublisher.ApiCache;

public class ApiCachePurgeCommandBuilder : IApiCachePurgeCommandBuilder
{
    /// <summary>
    /// Cloudflare allows up to 100 URLs per single-file purge request on every plan (500 on Enterprise);
    /// 30 was the long-standing lower limit, so it is used to stay safe. Today at most 4 URLs are produced.
    /// </summary>
    public const int MaxUrlsPerCommand = 30;

    /// <summary>
    /// The purge clears only Cloudflare's copy. The zone Browser Cache TTL (default 4 h) raises max-age to 14400
    /// on cached HITs, so a browser that got a HIT may keep the old copy for up to 4 h.
    /// </summary>
    public const string BrowserCacheNote =
        "Note: the purge does not clear browser caches. A browser that got a cached HIT may keep the old copy " +
        "for up to 4 h (zone Browser Cache TTL), so hard-reload (Ctrl+Shift+R) to check in a browser.";

    public ApiCachePurgeInstructions? Build(IEnumerable<ApiCachedContent> published)
    {
        var contents = published.Distinct().Order().ToList();
        if (contents.Count == 0)
        {
            return null;
        }

        var urls = ApiCacheRoutes.UrlsFor(contents);
        var commands = BuildCommands(urls);
        var verifyLines = contents
            .Select(content =>
                $"Verify: curl.exe -s -D - -o NUL {ApiCacheRoutes.ApiOrigins[0]}{ApiCacheRoutes.PathFor(content)} " +
                $"| Select-String '{ApiCacheRoutes.CacheHeaderFor(content)}' " +
                "- expect MISS on the first request after the purge (HIT means the old copy is still cached).")
            .Append(BrowserCacheNote)
            .ToList();
        return new ApiCachePurgeInstructions(urls, commands, verifyLines);
    }

    public string Format(ApiCachePurgeInstructions instructions)
    {
        const string rule = "======================================================================";
        var sb = new StringBuilder();
        sb.AppendLine(rule);
        sb.AppendLine("ACTION: purge the Api edge cache so the new R2 content is served now.");
        sb.AppendLine("Run in PowerShell on the machine with the Cloudflare CLI (cf):");
        sb.AppendLine();
        foreach (var command in instructions.Commands)
        {
            sb.AppendLine(command);
        }

        sb.AppendLine();
        foreach (var line in instructions.VerifyLines)
        {
            sb.AppendLine(line);
        }
        sb.Append(rule);
        return sb.ToString();
    }

    internal static IReadOnlyList<string> BuildCommands(IEnumerable<string> urls) =>
        urls.Chunk(MaxUrlsPerCommand).Select(ToCommand).ToList();

    /// <summary>
    /// JSON quotes are backslash-escaped so they survive PowerShell -> cf.cmd -> node argument parsing.
    /// URLs come only from <see cref="ApiCacheRoutes"/> (no quotes or backslashes), so no further escaping is needed.
    /// </summary>
    private static string ToCommand(string[] urls)
    {
        var files = string.Join(",", urls.Select(url => $"\\\"{url}\\\""));
        return $"cf cache purge -z {ApiCacheRoutes.Zone} --body '{{\\\"files\\\":[{files}]}}' --force";
    }
}
