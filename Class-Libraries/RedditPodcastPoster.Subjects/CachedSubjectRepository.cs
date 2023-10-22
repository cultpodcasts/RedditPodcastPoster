using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Subjects;

public class CachedSubjectRepository : ICachedSubjectRepository
{
    private readonly ILogger<CachedSubjectRepository> _logger;
    private readonly IRepository<Subject> _subjectRepository;
    private IList<Subject> Cache;
    private bool requiresFetch = true;

    public CachedSubjectRepository(
        IRepository<Subject> subjectRepository,
        ILogger<CachedSubjectRepository> logger)
    {
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Subject>> GetAll(string partitionKey)
    {
        if (requiresFetch)
        {
            await Fetch();
        }

        return Cache;
    }

    public async Task<Subject?> Get(string name, string partitionKey)
    {
        if (requiresFetch)
        {
            await Fetch();
        }

        return Cache.SingleOrDefault(x => x.Name == name);
    }

    public async Task Save(Subject data)
    {
        await _subjectRepository.Save(data);
        requiresFetch = true;
    }

    private async Task Fetch()
    {
        _logger.LogInformation($"Fetching {nameof(CachedSubjectRepository)}.");
        var subjects = await _subjectRepository.GetAll(Subject.PartitionKey);
        Cache = subjects.ToList();
        requiresFetch = false;
    }
}