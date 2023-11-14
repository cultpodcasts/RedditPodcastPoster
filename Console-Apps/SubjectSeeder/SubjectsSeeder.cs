using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace SubjectSeeder;

public class SubjectsSeeder
{
    private readonly ILogger<SubjectsSeeder> _logger;
    private readonly IRepository<Subject> _subjectRepository;
    private readonly ISubjectService _subjectService;

    public SubjectsSeeder(
        IRepository<Subject> subjectRepository,
        ISubjectService subjectService,
        ILogger<SubjectsSeeder> logger)
    {
        _subjectRepository = subjectRepository;
        _subjectService = subjectService;
        _logger = logger;
    }

    public async Task Run()
    {
        var newSubjects = new List<Subject>();
        foreach (var subject in newSubjects)
        {
            var match = await _subjectService.Match(subject);
            if (match == null)
            {
                await _subjectRepository.Save(subject);
            }
            else
            {
                _logger.LogError($"Subject '{subject.Name}' matches subject '{match.Name}'.");
            }
        }
    }
}