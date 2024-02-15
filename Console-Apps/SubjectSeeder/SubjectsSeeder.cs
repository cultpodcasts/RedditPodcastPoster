using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace SubjectSeeder;

public class SubjectsSeeder(
    ISubjectRepository subjectRepository,
    ISubjectService subjectService,
    ILogger<SubjectsSeeder> logger)
{
    public async Task Run()
    {
        var newSubjects = new List<Subject>
        {
            SubjectFactory.Create("Avatar", "Harry Palmer", "Enlightened Planetary Consciousness")
        };
        foreach (var subject in newSubjects)
        {
            var match = await subjectService.Match(subject);
            if (match == null)
            {
                await subjectRepository.Save(subject);
            }
            else
            {
                logger.LogError($"Subject '{subject.Name}' matches subject '{match.Name}'.");
            }
        }
    }
}