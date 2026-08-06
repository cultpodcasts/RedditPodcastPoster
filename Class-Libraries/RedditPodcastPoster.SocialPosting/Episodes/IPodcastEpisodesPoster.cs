using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.SocialPosting.Episodes;

public interface IPodcastEpisodesPoster
{
    Task<PostingResult> PostNewEpisodes(
        DateTime since,
        IEnumerable<PodcastEpisode> podcastEpisodes,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        bool preferYouTube = false,
        bool ignoreAppleGracePeriod = false,
        int? maxPosts = int.MaxValue);
}