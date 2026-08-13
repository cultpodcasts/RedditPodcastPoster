using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Models;
using Api.Services.Subjects;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Services;
using Xunit;
using SubjectEntity = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// Subject create/update persist flair fields to Cosmos without live Reddit sync (Reddit.NET retired).
/// </summary>
public class SubjectHandlerTests
{
    [Fact(DisplayName =
        "Plain English rule: when a subject is created with a Reddit flair template id, then save the subject and publish subjects without live Reddit flair sync, because Reddit.NET is retired.")]
    public async Task create_with_flair_id_saves_subject_without_live_reddit()
    {
        // Arrange
        var flairId = Guid.NewGuid();
        var entity = new SubjectEntity("Topic") { Id = Guid.NewGuid() };

        var subjectFactory = new Mock<ISubjectFactory>();
        subjectFactory.Setup(x => x.Create("Topic", null, null, null)).ReturnsAsync(entity);

        var subjectService = new Mock<ISubjectService>();
        subjectService.Setup(x => x.Match(It.IsAny<SubjectEntity>())).ReturnsAsync((SubjectEntity?)null);

        var subjectRepo = new Mock<ISubjectRepository>();
        subjectRepo.Setup(r => r.Save(It.IsAny<SubjectEntity>())).Returns(Task.CompletedTask);

        var publisher = new Mock<ISubjectsPublisher>();
        publisher.Setup(p => p.PublishSubjects()).Returns(Task.CompletedTask);

        var applier = CreateSubjectChangeApplier();
        var service = new SubjectCreateService(
            subjectRepo.Object,
            subjectService.Object,
            subjectFactory.Object,
            publisher.Object,
            applier,
            NullLogger<SubjectCreateService>.Instance);

        // Act
        var result = await service.CreateAsync(
            new SubjectChangeRequest { Name = "Topic", RedditFlairTemplateId = flairId },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectCreateStatus.Accepted);
        subjectRepo.Verify(
            x => x.Save(It.Is<SubjectEntity>(s => s.RedditFlairTemplateId == flairId)),
            Times.Once);
        publisher.Verify(x => x.PublishSubjects(), Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when a subject is updated with a Reddit flair template id, then persist the flair id on the subject without live Reddit sync, because Reddit.NET is retired.")]
    public async Task update_with_flair_id_saves_subject_without_live_reddit()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var flairId = Guid.NewGuid();
        var existing = new SubjectEntity("Topic") { Id = subjectId };

        var subjectRepo = new Mock<ISubjectRepository>();
        subjectRepo.Setup(x => x.GetBy(It.IsAny<Expression<Func<SubjectEntity, bool>>>()))
            .ReturnsAsync(existing);
        subjectRepo.Setup(r => r.Save(It.IsAny<SubjectEntity>())).Returns(Task.CompletedTask);

        var applier = CreateSubjectChangeApplier();
        var service = new SubjectUpdateService(
            subjectRepo.Object,
            applier,
            NullLogger<SubjectUpdateService>.Instance);

        // Act
        var result = await service.UpdateAsync(
            new SubjectChangeRequestWrapper(subjectId, new SubjectChangeRequest { RedditFlairTemplateId = flairId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectUpdateStatus.Accepted);
        existing.RedditFlairTemplateId.Should().Be(flairId);
        subjectRepo.Verify(x => x.Save(existing), Times.Once);
    }

    private static SubjectChangeApplier CreateSubjectChangeApplier() =>
        new(NullLogger<SubjectChangeApplier>.Instance);
}
