using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Stores;

public interface IYouTubeIndexerKeyStateStore
{
    Task<YouTubeIndexerKeyState?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(YouTubeIndexerKeyState state, CancellationToken cancellationToken = default);
}
