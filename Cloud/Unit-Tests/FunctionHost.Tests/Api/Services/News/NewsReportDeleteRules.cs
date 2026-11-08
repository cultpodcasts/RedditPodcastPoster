using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

public class NewsReportDeleteRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<PodcastEpisode> _shortLinkDeletes = [];

    public NewsReportDeleteRules()
    {
        _mocker.Use(NullLogger<NewsReportService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
    }

    private NewsReport CreateReport(Action<NewsReport>? customise = null)
    {
        var organisationId = _fixture.CreateGuid();
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            NewsOrganisationId = organisationId,
            NewsOrganisationName = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration()
        };
        customise?.Invoke(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(report);
        return report;
    }

    [Fact(DisplayName =
        "Delete news report returns NotFound when the report is missing, because nothing can be removed.")]
    public async Task delete_returns_not_found_when_missing()
    {
        // Arrange
        var missingId = _fixture.CreateGuid();
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((NewsReport?)null);
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.DeleteAsync(missingId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.NotFound);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Delete news report returns AlreadySocial when the report is tweeted, because social posts must be cleared first.")]
    public async Task delete_returns_already_social_when_tweeted()
    {
        // Arrange
        var report = CreateReport(r => r.OldTweeted = true);
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.DeleteAsync(report.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.AlreadySocial);
        result.Tweeted.Should().BeTrue();
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Delete news report removes the Cosmos row when not tweeted, so an admin can finish soft-remove with a hard delete.")]
    public async Task delete_success_deletes_report()
    {
        // Arrange
        var report = CreateReport();
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.DeleteAsync(report.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.Delete(report.NewsOrganisationId, report.Id), Times.Once);
        _search.VerifyDelete(report.Id, Times.Once());
        _shortLinkDeletes.Should().ContainSingle().Which.Episode.Id.Should().Be(report.Id);
    }
}
