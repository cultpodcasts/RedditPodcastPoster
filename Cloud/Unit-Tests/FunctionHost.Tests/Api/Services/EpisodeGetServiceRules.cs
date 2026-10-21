using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class EpisodeGetServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Plain English rule: when the route's podcast name does not match the episode's podcast, then the get fails and the mismatch exception is logged as an error, because it must appear in App Insights exceptions and must not be reported as a 404.")]
    public async Task get_logs_mismatch_exception_as_error_and_fails()
    {
        // Arrange
        var mismatch = new PodcastEpisodeMismatchException(
            _fixture.CreateGuid(), _fixture.CreateTitle(), _fixture.CreateGuid(), _fixture.CreateTitle());
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ThrowsAsync(mismatch);
        var sut = _mocker.CreateInstance<EpisodeGetService>();

        // Act
        var result = await sut.GetAsync(
            new PodcastEpisodeRequestWrapper(_fixture.CreateTitle(), mismatch.EpisodeId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeGetStatus.Failed);
        _mocker.GetMock<ILogger<EpisodeGetService>>().Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                mismatch,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when the resolver finds no episode, then the get reports the episode as not found and logs no error, because an unknown episode-id is a normal 404.")]
    public async Task get_reports_unknown_episode_as_not_found_without_error()
    {
        // Arrange
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.EpisodeNotFound));
        var sut = _mocker.CreateInstance<EpisodeGetService>();

        // Act
        var result = await sut.GetAsync(
            new PodcastEpisodeRequestWrapper(_fixture.CreateTitle(), _fixture.CreateGuid()), CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeGetStatus.EpisodeNotFound);
        _mocker.GetMock<ILogger<EpisodeGetService>>().Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}