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

    public Expression<Func<Episode, bool>> StoredUrlEquals(Uri url)
    {
        var iplayer = ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BbcIplayer, url);
        var sounds = ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BbcSounds, url);
        return episode =>
            episode.Services != null &&
            (episode.Services[ServiceKeys.BbcIplayer].Url == iplayer ||
             episode.Services[ServiceKeys.BbcSounds].Url == sounds);
    }

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url)
    {
        var iplayer = ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BbcIplayer, url);
        var sounds = ServiceCatalog.CanonicalUrlOrSelf(ServiceKeys.BbcSounds, url);
        return episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer) == iplayer ||
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds) == sounds);
    }

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) =>
        bbcPageMetaDataExtractor.GetMetaData(url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url, string html) =>
        throw new NotSupportedException(
            "HTML extract is not registered for BBC; Browser Rendering allowlist starts at itvx only.");
}
