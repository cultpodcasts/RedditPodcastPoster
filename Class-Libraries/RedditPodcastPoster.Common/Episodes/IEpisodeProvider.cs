using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IndexingContext indexingContext);
}