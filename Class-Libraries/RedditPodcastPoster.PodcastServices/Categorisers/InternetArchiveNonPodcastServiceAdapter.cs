using System.Linq.Expressions;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Categorisers;

public class InternetArchiveNonPodcastServiceAdapter(
    IInternetArchivePageMetaDataExtractor internetArchivePageMetaDataExtractor
) : INonPodcastServiceAdapter
{
    public NonPodcastService Service => NonPodcastService.InternetArchive;

    public bool IsSubmitUrl(Uri url) => InternetArchiveUrlMatcher.IsSubmitUrl(url);

    public bool CanExtract(Uri url) => InternetArchiveUrlMatcher.IsInternetArchiveUrl(url);

    public Expression<Func<Episode, bool>> StoredUrlEquals(Uri url) =>
        episode =>
            episode.Services != null &&
            episode.Services[ServiceKeys.InternetArchive].Url == url;

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url) =>
        episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.InternetArchive) == url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) =>
        internetArchivePageMetaDataExtractor.GetMetaData(url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url, string html) =>
        throw new NotSupportedException(
            "HTML extract is not registered for Internet Archive; Browser Rendering allowlist starts at itvx only.");
}
