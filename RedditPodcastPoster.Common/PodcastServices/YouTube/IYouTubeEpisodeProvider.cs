using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(Podcast podcast, DateTime? processRequestReleasedSince);
}