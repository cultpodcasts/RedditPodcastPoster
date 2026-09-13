using System.Linq.Expressions;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

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
        var iplayer = StreamingServiceCatalog.CanonicalUrlOrSelf(StreamingServiceKeys.BbcIplayer, url);
        var sounds = StreamingServiceCatalog.CanonicalUrlOrSelf(StreamingServiceKeys.BbcSounds, url);
        return episode =>
            episode.Services != null &&
            (episode.Services[StreamingServiceKeys.BbcIplayer].Url == iplayer ||
             episode.Services[StreamingServiceKeys.BbcSounds].Url == sounds);
    }

    public Episode? FindMatchingEpisode(IEnumerable<Episode> episodes, Uri url)
    {
        var iplayer = StreamingServiceCatalog.CanonicalUrlOrSelf(StreamingServiceKeys.BbcIplayer, url);
        var sounds = StreamingServiceCatalog.CanonicalUrlOrSelf(StreamingServiceKeys.BbcSounds, url);
        return episodes.FirstOrDefault(episode =>
            EpisodeServicePresence.TryGetUrl(episode, StreamingServiceKeys.BbcIplayer) == iplayer ||
            EpisodeServicePresence.TryGetUrl(episode, StreamingServiceKeys.BbcSounds) == sounds);
    }

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url) =>
        bbcPageMetaDataExtractor.GetMetaData(url);

    public Task<NonPodcastServiceItemMetaData> ExtractMetaData(Uri url, string html) =>
        throw new NotSupportedException(
            "HTML extract is not registered for BBC; Browser Rendering allowlist starts at itvx only.");
}
