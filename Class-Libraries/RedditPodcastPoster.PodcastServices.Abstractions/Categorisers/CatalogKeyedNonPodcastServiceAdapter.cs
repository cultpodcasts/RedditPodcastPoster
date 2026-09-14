using System.Linq.Expressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

/// <summary>
/// Adapter for a catalog-keyed streaming destination. Service plugins register one of these
/// so PodcastServices does not grow a switch (or HTML-scraping references) per host.
/// </summary>
public class CatalogKeyedNonPodcastServiceAdapter(
    StreamingService service,
    Func<Uri, bool> isSubmitUrl,
    Func<Uri, bool> canExtract,
    Func<Uri, Task<NonPodcastServiceItemMetaData>> extract,
    Func<Uri, string, Task<NonPodcastServiceItemMetaData>>? extractFromHtml = null,
    Func<Uri, Uri>? canonicalizeUrl = null
) : INonPodcastServiceAdapter
{
    private readonly string _catalogKey = StreamingServiceWire.ToKey(service);

    public StreamingService ResolveService(Uri url) => service;

    public bool IsSubmitUrl(Uri url) => isSubmitUrl(url);

    public bool CanExtract(Uri url) => canExtract(url);

    public Expression<Func<Episode, bool>> StoredUrlEquals(Uri url)
    {
        var stored = CanonicalStoredUrl(url);
        var catalogKey = _catalogKey;
        return episode => episode.Services != null && episode.Services[catalogKey].Url == stored;
    }

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url)
    {
        var stored = CanonicalStoredUrl(url);
        return episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, service) == stored);
    }

    private Uri CanonicalStoredUrl(Uri url) =>
        canonicalizeUrl?.Invoke(url) ?? StreamingServiceCatalog.CanonicalUrlOrSelf(_catalogKey, url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) => extract(url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url, string html) =>
        extractFromHtml != null
            ? extractFromHtml(url, html)
            : throw new NotSupportedException(
                $"HTML extract is not registered for service '{service}'.");
}
