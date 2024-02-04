using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subreddit;

public class SubredditRepository(
    IFileRepository fileRepository,
    ILogger<SubredditRepository> logger)
    : ISubredditRepository
{
    public async Task<IEnumerable<RedditPost>> GetAll()
    {
        return await fileRepository.GetAll<RedditPost>().ToArrayAsync();
    }

    public Task Save(RedditPost post)
    {
        return fileRepository.Write(post);
    }
}