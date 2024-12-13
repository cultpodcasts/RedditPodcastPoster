using Api.Models;
using RedditPodcastPoster.Models;

namespace Api
{
    public interface IImageUpdater
    {
        Task UpdateImages(Podcast podcast, Episode episode, EpisodeChangeState changeState);
    }
}
