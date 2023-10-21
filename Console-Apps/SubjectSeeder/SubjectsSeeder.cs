using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;

namespace SubjectSeeder;

public class SubjectsSeeder
{
    private readonly ILogger<SubjectsSeeder> _logger;
    private readonly IRepository<Subject> _subjectRepository;

    public SubjectsSeeder(
        IRepository<Subject> subjectRepository,
        ILogger<SubjectsSeeder> logger)
    {
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var subjects = new List<Subject>();
        foreach (var subject in subjects)
        {
            await _subjectRepository.Save(subject);
        }
    }
}