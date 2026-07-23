using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Subjects;

public class SubjectGetService(
    ISubjectRepository subjectRepository,
    ILogger<SubjectGetService> logger) : ISubjectGetService
{
    public async Task<SubjectGetResult> GetAsync(string subjectName, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Get subject '{subjectName}'.", subjectName);
            var subject = await subjectRepository.GetBy(x => x.Name == subjectName);
            if (subject == null)
            {
                logger.LogInformation("Could not find subject with name '{subjectName}'.", subjectName);
                return new SubjectGetResult(SubjectGetStatus.NotFound);
            }

            return new SubjectGetResult(SubjectGetStatus.Ok, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get subject.", nameof(GetAsync));
            return new SubjectGetResult(SubjectGetStatus.Failed);
        }
    }
}
