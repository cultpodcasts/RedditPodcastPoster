using System.Text.Json;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace CataloguePlayableServicesBackfill;

/// <summary>
/// One hit from a PublicDatabase checkout: the file that held the id and its <c>urls</c> map
/// (public files store urls only — not service images).
/// </summary>
public sealed record PublicUrlHit(string FilePath, IReadOnlyDictionary<string, Uri> Urls);

/// <summary>
/// Indexes playable / episode ids from a PublicDatabase folder (flat <c>*.json</c> podcast, TV show,
/// news organisation and film files) for GUID join during services backfill.
/// </summary>
public interface IPublicDatabaseUrlIndex
{
    bool TryGet(Guid id, out PublicUrlHit hit);
}

public sealed class EmptyPublicDatabaseUrlIndex : IPublicDatabaseUrlIndex
{
    public static EmptyPublicDatabaseUrlIndex Instance { get; } = new();

    public bool TryGet(Guid id, out PublicUrlHit hit)
    {
        hit = null!;
        return false;
    }
}

public sealed class FilePublicDatabaseUrlIndex : IPublicDatabaseUrlIndex
{
    private readonly Dictionary<Guid, PublicUrlHit> _byId;

    private FilePublicDatabaseUrlIndex(Dictionary<Guid, PublicUrlHit> byId) => _byId = byId;

    public static FilePublicDatabaseUrlIndex Load(string publicDatabaseDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicDatabaseDir);
        var root = Path.GetFullPath(publicDatabaseDir);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Public database dir not found: {root}");
        }

        var byId = new Dictionary<Guid, PublicUrlHit>();
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly))
        {
            IndexFile(path, byId);
        }

        return new FilePublicDatabaseUrlIndex(byId);
    }

    public bool TryGet(Guid id, out PublicUrlHit hit) => _byId.TryGetValue(id, out hit!);

    private static void IndexFile(string path, Dictionary<Guid, PublicUrlHit> byId)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("episodes", out var episodes) && episodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in episodes.EnumerateArray())
                {
                    TryIndexChild(path, child, byId);
                }

                return;
            }

            if (root.TryGetProperty("reports", out var reports) && reports.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in reports.EnumerateArray())
                {
                    TryIndexChild(path, child, byId);
                }

                return;
            }

            // Film (and any other singleton public file with id + urls).
            TryIndexChild(path, root, byId);
        }
    }

    private static void TryIndexChild(string path, JsonElement element, Dictionary<Guid, PublicUrlHit> byId)
    {
        if (!element.TryGetProperty("id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(idElement.GetString(), out var id))
        {
            return;
        }

        if (!element.TryGetProperty("urls", out var urlsElement) || urlsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var urls = ReadUrls(urlsElement);
        if (urls.Count == 0)
        {
            return;
        }

        // First hit wins; public files should not duplicate ids across parents.
        byId.TryAdd(id, new PublicUrlHit(path, urls));
    }

    /// <summary>
    /// Reads either a catalogue-key map (<c>bbcIplayer</c>, <c>youtube</c>, …) or the podcast leftover shape
    /// (<c>spotify</c>/<c>apple</c>/<c>youtube</c>/<c>internetArchive</c>/<c>bbc</c>).
    /// </summary>
    internal static Dictionary<string, Uri> ReadUrls(JsonElement urlsElement)
    {
        var urls = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var property in urlsElement.EnumerateObject())
        {
            if (!TryReadUri(property.Value, out var uri) || uri is null)
            {
                continue;
            }

            var key = property.Name;
            if (string.Equals(key, "bbc", StringComparison.OrdinalIgnoreCase))
            {
                key = StreamingServiceCatalog.TryResolveKey(uri)
                      ?? StreamingServiceWire.ToKey(StreamingService.BbcSounds);
            }

            urls[key] = uri;
        }

        return urls;
    }

    private static bool TryReadUri(JsonElement element, out Uri? uri)
    {
        uri = null;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text) ||
            !Uri.TryCreate(text, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
