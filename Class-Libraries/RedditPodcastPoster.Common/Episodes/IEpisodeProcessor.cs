namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProcessor
{
    Task<ProcessResponse> PostEpisodesSinceReleaseDate(DateTime since, bool youTubeRefreshed, bool spotifyRefreshed);
}