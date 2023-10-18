using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFilter
{
    FilterResult Filter(Podcast podcast, List<string> eliminationTerms);
}