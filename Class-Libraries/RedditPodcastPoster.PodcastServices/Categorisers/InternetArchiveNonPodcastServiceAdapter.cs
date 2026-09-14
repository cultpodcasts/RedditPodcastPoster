using System.Linq.Expressions;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.PodcastServices.Categorisers;

public class InternetArchiveNonPodcastServiceAdapter(
    IInternetArchivePageMetaDataExtractor internetArchivePageMetaDataExtractor
) : INonPodcastServiceAdapter
{
    public StreamingService ResolveService(Uri url) => StreamingService.InternetArchive;

    public bool IsSubmitUrl(Uri url) => InternetArchiveUrlMatcher.IsSubmitUrl(url);

    public bool CanExtract(Uri url) => InternetArchiveUrlMatcher.IsInternetArchiveUrl(url);

    public Expression<Func<Episode, bool>> StoredUrlEquals(Uri url)
    {
        var key = StreamingServiceWire.ToKey(StreamingService.InternetArchive);
        var stored = StreamingServiceCatalog.CanonicalUrlOrSelf(key, url);
        return episode =>
            episode.Services != null &&
            episode.Services[key].Url == stored;
    }

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url)
    {
        var stored = StreamingServiceCatalog.CanonicalUrlOrSelf(
            StreamingServiceWire.ToKey(StreamingService.InternetArchive), url);
        return episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, StreamingService.InternetArchive) == stored);
    }

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) =>
        internetArchivePageMetaDataExtractor.GetMetaData(url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url, string html) =>
        throw new NotSupportedException(
            "HTML extract is not registered for Internet Archive; Browser Rendering allowlist starts at itvx only.");
}
