using System.Text.Json;
using Api.Dtos;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Services;

namespace Api.Services.Subjects;

public class SubjectCreateService(
    ISubjectRepository subjectRepository,
    ISubjectService subjectService,
    ISubjectFactory subjectFactory,
    ISubjectsPublisher contentPublisher,
    SubjectChangeApplier subjectChangeApplier,
    ILogger<SubjectCreateService> logger) : ISubjectCreateService
{
    public async Task<SubjectCreateResult> CreateAsync(Subject subject, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{method}: received subject: {subject}",
                nameof(CreateAsync), JsonSerializer.Serialize(subject));
            if (string.IsNullOrWhiteSpace(subject.Name))
            {
                logger.LogWarning("Missing subject-name.");
                return new SubjectCreateResult(
                    SubjectCreateStatus.BadRequest,
                    Message: "Missing subject-name");
            }

            var entity = await subjectFactory.Create(subject.Name);
            await subjectChangeApplier.Apply(entity, subject);
            var matchingSubject = await subjectService.Match(entity);
            if (matchingSubject != null)
            {
                return new SubjectCreateResult(
                    SubjectCreateStatus.Conflict,
                    ConflictName: matchingSubject.Name);
            }

            await subjectRepository.Save(entity);
            await contentPublisher.PublishSubjects();
            logger.LogInformation("Created subject '{subjectName}' with subject-id '{subjectId}'.",
                subject.Name, subject.Id);

            return new SubjectCreateResult(SubjectCreateStatus.Accepted, entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to create subject.", nameof(CreateAsync));
            return new SubjectCreateResult(SubjectCreateStatus.Failed);
        }
    }
}
