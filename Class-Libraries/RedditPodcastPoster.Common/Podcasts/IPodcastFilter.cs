using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFilter
{
    FilterResult Filter(Podcast podcast, IEnumerable<Episode> episodes, List<string> eliminationTerms);
}
