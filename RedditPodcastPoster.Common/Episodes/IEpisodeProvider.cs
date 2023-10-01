using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IndexOptions indexOptions);
}