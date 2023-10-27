using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class CachedSubjectRepository : ICachedSubjectRepository
{
    private readonly ILogger<CachedSubjectRepository> _logger;
    private readonly IRepository<Subject> _subjectRepository;
    private IList<Subject> _cache = new List<Subject>();
    private bool _requiresFetch = true;

    public CachedSubjectRepository(
        IRepository<Subject> subjectRepository,
        ILogger<CachedSubjectRepository> logger)
    {
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Subject>> GetAll(string partitionKey)
    {
        if (_requiresFetch)
        {
            await Fetch();
        }

        return _cache;
    }

    public async Task<Subject?> Get(string name, string partitionKey)
    {
        if (_requiresFetch)
        {
            await Fetch();
        }

        return _cache.SingleOrDefault(x => x.Name == name);
    }

    public async Task Save(Subject data)
    {
        await _subjectRepository.Save(data);
        _requiresFetch = true;
    }

    private async Task Fetch()
    {
        _logger.LogInformation($"Fetching {nameof(CachedSubjectRepository)}.");
        var subjects = await _subjectRepository.GetAll(Subject.PartitionKey);
        _cache = subjects.ToList();
        _requiresFetch = false;
        if (_cache.Any(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            foreach (var subject in _cache.Where(x => string.IsNullOrWhiteSpace(x.Name)))
            {
                _logger.LogError($"Retrieved a subject with empty name, has id {subject.Id}");
            }

            throw new InvalidOperationException("Retrieved subjects with null/empty name");
        }
    }
}