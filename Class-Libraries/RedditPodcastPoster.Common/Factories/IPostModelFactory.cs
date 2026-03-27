using RedditPodcastPoster.Models;
using Episode = RedditPodcastPoster.Models.Episode;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Common.Factories;

public interface IPostModelFactory
{
    PostModel ToPostModel(
        (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes,
        bool preferYouTube = false);
}