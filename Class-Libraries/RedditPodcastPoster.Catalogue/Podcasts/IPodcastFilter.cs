using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Catalogue.Podcasts;

public interface IPodcastFilter
{
    FilterResult Filter(Podcast podcast, IEnumerable<Episode> episodes, List<string> eliminationTerms);
}
