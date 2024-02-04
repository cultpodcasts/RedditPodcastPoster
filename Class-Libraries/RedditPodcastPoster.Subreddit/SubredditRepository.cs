using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subreddit;

public class SubredditRepository(
    IFileRepository fileRepository,
    ILogger<SubredditRepository> logger)
    : ISubredditRepository
{
    public IAsyncEnumerable<RedditPost> GetAll()
    {
        return fileRepository.GetAll<RedditPost>();
    }

    public Task Save(RedditPost post)
    {
        return fileRepository.Write(post);
    }
}