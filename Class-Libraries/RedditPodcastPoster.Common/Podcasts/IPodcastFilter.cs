using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFilter
{
    FilterResult Filter(Podcast podcast, IEnumerable<Episode> episodes, List<string> eliminationTerms);
}