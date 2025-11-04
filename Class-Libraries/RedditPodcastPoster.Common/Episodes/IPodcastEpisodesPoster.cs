namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodesPoster
{
    Task<IList<ProcessResponse>> PostNewEpisodes(
        DateTime since,
        IEnumerable<Guid> podcastIds,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        bool preferYouTube = false,
        bool ignoreAppleGracePeriod = false,
        int? maxPosts = int.MaxValue);
}