using System.Linq.Expressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

/// <summary>
/// Adapter for a catalog-keyed streaming destination. Service plugins register one of these
/// so PodcastServices does not grow a switch (or HTML-scraping references) per host.
/// </summary>
public class CatalogKeyedNonPodcastServiceAdapter(
    NonPodcastService service,
    string catalogKey,
    Func<Uri, bool> isSubmitUrl,
    Func<Uri, bool> canExtract,
    Func<Uri, Task<NonPodcastServiceItemMetaData>> extract
) : INonPodcastServiceAdapter
{
    public NonPodcastService Service { get; } = service;

    public bool IsSubmitUrl(Uri url) => isSubmitUrl(url);

    public bool CanExtract(Uri url) => canExtract(url);

    public Expression<Func<Episode, bool>> StoredUrlEquals(Uri url) =>
        episode => episode.Services != null && episode.Services[catalogKey].Url == url;

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url) =>
        episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, catalogKey) == url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) => extract(url);
}
