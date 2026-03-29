using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Factories;

public interface IPostModelFactory
{
    PostModel ToPostModel(
        (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes,
        bool preferYouTube = false);
}