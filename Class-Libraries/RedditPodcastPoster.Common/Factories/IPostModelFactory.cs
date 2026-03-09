using RedditPodcastPoster.Models;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Factories;

public interface IPostModelFactory
{
    PostModel ToPostModel(
        (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes,
        bool preferYouTube = false);
}