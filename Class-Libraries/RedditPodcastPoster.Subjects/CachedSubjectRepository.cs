using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class CachedSubjectRepository : ICachedSubjectRepository
{
    private readonly ILogger<CachedSubjectRepository> _logger;
    private readonly ISubjectRepository _subjectRepository;
    private IList<Subject> _cache = new List<Subject>();
    private bool _requiresFetch = true;

    public CachedSubjectRepository(
        ISubjectRepository subjectRepository,
        ILogger<CachedSubjectRepository> logger)
    {
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Subject>> GetAll()
    {
        if (_requiresFetch)
        {
            await Fetch();
        }

        return _cache;
    }

    private async Task Fetch()
    {
        _logger.LogInformation($"Fetching {nameof(CachedSubjectRepository)}.");
        var subjects = await _subjectRepository.GetAll();
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