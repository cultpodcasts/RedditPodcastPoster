using System.Text.Json;
using Api.Dtos;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Subjects;

public class SubjectUpdateService(
    ISubjectRepository subjectRepository,
    SubjectChangeApplier subjectChangeApplier,
    ILogger<SubjectUpdateService> logger) : ISubjectUpdateService
{
    public async Task<SubjectUpdateResult> UpdateAsync(
        SubjectChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "{method} Subject Change Request: episode-id: '{SubjectId}'. {subjectJson}",
                nameof(UpdateAsync), request.SubjectId,
                JsonSerializer.Serialize(request.Subject));
            var subject = await subjectRepository.GetBy(x => x.Id == request.SubjectId);
            if (subject == null)
            {
                return new SubjectUpdateResult(SubjectUpdateStatus.NotFound);
            }

            logger.LogInformation(
                "{method} Updating subject-id '{SubjectId}'. Original-episode: {subject}",
                nameof(UpdateAsync), request.SubjectId, JsonSerializer.Serialize(subject));

            await subjectChangeApplier.Apply(subject, request.Subject);
            await subjectRepository.Save(subject);
            return new SubjectUpdateResult(SubjectUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update subject.", nameof(UpdateAsync));
            return new SubjectUpdateResult(SubjectUpdateStatus.Failed);
        }
    }
}
