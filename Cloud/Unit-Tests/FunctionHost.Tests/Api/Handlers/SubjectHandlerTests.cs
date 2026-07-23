using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Api.Models;
using Api.Services.Subjects;
using Reddit.Exceptions;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Reddit.Clients;
using RedditPodcastPoster.Reddit.Configuration;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Services;
using Xunit;
using SubjectEntity = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// Service-level tests preserving #914 intent: Reddit Forbidden flair errors must not block subject persistence.
/// </summary>
public class SubjectHandlerTests
{
    [Fact(DisplayName =
        "Plain English rule: when Reddit flair update is forbidden during subject create, then still save the subject and publish subjects, because flair sync failure must not block curation.")]
    public async Task create_when_reddit_flair_forbidden_still_saves_subject()
    {
        // Arrange
        var flairId = Guid.NewGuid();
        var entity = new SubjectEntity("Topic") { Id = Guid.NewGuid() };

        var subjectFactory = new Mock<ISubjectFactory>();
        subjectFactory.Setup(x => x.Create("Topic", null, null, null)).ReturnsAsync(entity);

        var subjectService = new Mock<ISubjectService>();
        subjectService.Setup(x => x.Match(It.IsAny<SubjectEntity>())).ReturnsAsync((SubjectEntity?)null);

        var redditClient = new Mock<IAdminRedditClient>();
        redditClient.Setup(x => x.Client)
            .Throws(new RedditForbiddenException("Reddit API returned Forbidden (403) response."));

        var subjectRepo = new Mock<ISubjectRepository>();
        subjectRepo.Setup(r => r.Save(It.IsAny<SubjectEntity>())).Returns(Task.CompletedTask);

        var publisher = new Mock<ISubjectsPublisher>();
        publisher.Setup(p => p.PublishSubjects()).Returns(Task.CompletedTask);

        var applier = CreateSubjectChangeApplier(subjectRepo.Object, redditClient.Object);
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
        "Plain English rule: when Reddit flair update is forbidden during subject update, then still save the subject with the requested flair, because flair sync failure must not roll back local changes.")]
    public async Task update_when_reddit_flair_forbidden_still_saves_subject()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var flairId = Guid.NewGuid();
        var existing = new SubjectEntity("Topic") { Id = subjectId };

        var subjectRepo = new Mock<ISubjectRepository>();
        subjectRepo.Setup(x => x.GetBy(It.IsAny<Expression<Func<SubjectEntity, bool>>>()))
            .ReturnsAsync(existing);
        subjectRepo.Setup(r => r.Save(It.IsAny<SubjectEntity>())).Returns(Task.CompletedTask);

        var redditClient = new Mock<IAdminRedditClient>();
        redditClient.Setup(x => x.Client)
            .Throws(new RedditForbiddenException("Reddit API returned Forbidden (403) response."));

        var applier = CreateSubjectChangeApplier(subjectRepo.Object, redditClient.Object);
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

    private static SubjectChangeApplier CreateSubjectChangeApplier(
        ISubjectRepository subjectRepository,
        IAdminRedditClient redditClient) =>
        new(
            subjectRepository,
            redditClient,
            Options.Create(new SubredditSettings { SubredditName = "testsubreddit" }),
            NullLogger<SubjectChangeApplier>.Instance);
}
