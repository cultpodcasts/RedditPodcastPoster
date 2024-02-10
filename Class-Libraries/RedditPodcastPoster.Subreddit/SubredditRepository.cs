using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subreddit;

public class SubredditRepository(
    IFileRepository fileRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SubredditRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
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