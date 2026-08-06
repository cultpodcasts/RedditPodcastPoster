using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.SocialPosting.Factories;

public interface IPostModelFactory
{
    PostModel ToPostModel(
        (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes,
        bool preferYouTube = false);
}
