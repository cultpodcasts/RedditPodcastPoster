using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class CachedSubjectProvider(
    ISubjectRepository subjectRepository,
    ILogger<CachedSubjectProvider> logger)
    : ISubjectsProvider, ICachedSubjectProvider
{
    private IList<Subject> _cache = new List<Subject>();
    private bool _requiresFetch = true;

    public async IAsyncEnumerable<Subject> GetAll()
    {
        if (_requiresFetch)
        {
            await Fetch();
        }

        foreach (var subject in _cache)
        {
            yield return subject;
        }
    }

    private async Task Fetch()
    {
        logger.LogInformation($"Fetching {nameof(CachedSubjectProvider)}.");
        var subjects = await subjectRepository.GetAll().ToArrayAsync();
        _cache = subjects.ToList();
        _requiresFetch = false;
        if (_cache.Any(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            foreach (var subject in _cache.Where(x => string.IsNullOrWhiteSpace(x.Name)))
            {
                logger.LogError("Retrieved a subject with empty name, has id {subjectId}", subject.Id);
            }

            throw new InvalidOperationException("Retrieved subjects with null/empty name");
        }
    }
}