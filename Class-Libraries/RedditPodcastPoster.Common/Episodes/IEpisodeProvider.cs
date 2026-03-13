using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        IndexingContext indexingContext);
}