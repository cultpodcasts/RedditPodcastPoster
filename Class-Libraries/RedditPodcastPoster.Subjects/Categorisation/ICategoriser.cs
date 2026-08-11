using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Subjects.Categorisation;

public interface ICategoriser
{
    Task<bool> Categorise(Episode episode, Podcast podcast);
}
