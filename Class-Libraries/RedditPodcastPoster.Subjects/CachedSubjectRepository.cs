using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class CachedSubjectRepository(
    ISubjectRepository subjectRepository,
    ILogger<CachedSubjectRepository> logger)
    : ICachedSubjectRepository
{
    private IList<Subject> _cache = new List<Subject>();
    private bool _requiresFetch = true;

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
        logger.LogInformation($"Fetching {nameof(CachedSubjectRepository)}.");
        var subjects = await subjectRepository.GetAll();
        _cache = subjects.ToList();
        _requiresFetch = false;
        if (_cache.Any(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            foreach (var subject in _cache.Where(x => string.IsNullOrWhiteSpace(x.Name)))
            {
                logger.LogError($"Retrieved a subject with empty name, has id {subject.Id}");
            }

            throw new InvalidOperationException("Retrieved subjects with null/empty name");
        }
    }
}