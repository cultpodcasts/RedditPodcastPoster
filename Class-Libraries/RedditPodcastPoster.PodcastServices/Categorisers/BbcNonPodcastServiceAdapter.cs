using System.Linq.Expressions;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Categorisers;

public class BbcNonPodcastServiceAdapter(
    IBBCPageMetaDataExtractor bbcPageMetaDataExtractor
) : INonPodcastServiceAdapter
{
    public NonPodcastService Service => NonPodcastService.BBC;

    public bool IsSubmitUrl(Uri url) => BBCUrlMatcher.IsSubmitUrl(url);

    public bool CanExtract(Uri url) => BBCUrlMatcher.IsBBCUrl(url);

    public Expression<Func<Episode, bool>> StoredUrlEquals(Uri url) =>
        episode =>
            episode.Services != null &&
            (episode.Services[ServiceKeys.BbcIplayer].Url == url ||
             episode.Services[ServiceKeys.BbcSounds].Url == url);

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url) =>
        episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer) == url ||
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds) == url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) =>
        bbcPageMetaDataExtractor.GetMetaData(url);
}
