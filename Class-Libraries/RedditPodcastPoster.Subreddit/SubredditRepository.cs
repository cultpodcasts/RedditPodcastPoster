using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Subreddit;

public class SubredditRepository : ISubredditRepository
{
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<SubredditRepository> _logger;

    public SubredditRepository(
        IFileRepository fileRepository,
        ILogger<SubredditRepository> logger)
    {
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<RedditPost>> GetAll()
    {
        return await _fileRepository.GetAll<RedditPost>(RedditPost.PartitionKey).ToArrayAsync();
    }

    public async Task Save(RedditPost post)
    {
        await _fileRepository.Write(post.FullName, post);
    }
}