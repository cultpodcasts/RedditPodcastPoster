using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public interface IUrlCategoriser
{
    Task<CategorisedItem> Categorise(
        Podcast? podcast,
        Uri url,
        IndexingContext indexingContext,
        bool matchOtherServices,
        NonPodcastServiceItemMetaData? prefetchedMeta = null,
        bool forceMetaExtract = false);

    /// <summary>
    /// As <see cref="Categorise(Podcast?, Uri, IndexingContext, bool, NonPodcastServiceItemMetaData?, bool)"/>, but
    /// matches the URL against <paramref name="knownEpisodes"/> instead of loading the podcast's episodes. Used for a
    /// news organisation / TV show run as a transient podcast, whose items are its stored playables.
    /// </summary>
    Task<CategorisedItem> Categorise(
        Podcast podcast,
        IReadOnlyList<Episode> knownEpisodes,
        Uri url,
        IndexingContext indexingContext,
        bool matchOtherServices,
        NonPodcastServiceItemMetaData? prefetchedMeta,
        bool forceMetaExtract);
}
