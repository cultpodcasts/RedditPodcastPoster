using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        IndexingContext indexingContext);
}
