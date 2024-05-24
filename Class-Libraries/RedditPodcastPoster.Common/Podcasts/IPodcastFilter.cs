using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFilter
{
    FilterResult Filter(Podcast podcast, List<string> eliminationTerms);
}