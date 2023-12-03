using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodesPoster
{
    Task<IList<ProcessResponse>> PostNewEpisodes(
        DateTime since,
        IList<Podcast> podcasts,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        bool preferYouTube = false);
}