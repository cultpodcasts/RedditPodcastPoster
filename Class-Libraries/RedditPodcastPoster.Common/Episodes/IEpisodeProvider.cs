using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        IndexingContext indexingContext);
}