using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFilter
{
    void Filter(Podcast podcast, List<string> eliminationTerms);
}