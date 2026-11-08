using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowEpisodeDeleteRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();

    public TvShowEpisodeDeleteRules()
    {
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<RedditPodcastPoster.Models.Episodes.PodcastEpisode>()))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
    }

    private TvShowEpisode CreateEpisode(Action<TvShowEpisode>? customise = null)
    {
        var showId = _fixture.CreateGuid();
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            TvShowId = showId,
            TvShowName = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration()
        };
        customise?.Invoke(episode);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        return episode;
    }

    [Fact(DisplayName =
        "Delete TV episode returns NotFound when the episode is missing, because nothing can be removed.")]
    public async Task delete_returns_not_found_when_missing()
    {
        // Arrange
        var missingId = _fixture.CreateGuid();
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((TvShowEpisode?)null);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.DeleteAsync(missingId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.NotFound);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Delete TV episode returns AlreadySocial when the episode is tweeted, because social posts must be cleared first.")]
    public async Task delete_returns_already_social_when_tweeted()
    {
        // Arrange
        var episode = CreateEpisode(e => e.OldTweeted = true);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.DeleteAsync(episode.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.AlreadySocial);
        result.Tweeted.Should().BeTrue();
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Delete TV episode removes the Cosmos row when not tweeted, so an admin can finish soft-remove with a hard delete.")]
    public async Task delete_success_deletes_episode()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.DeleteAsync(episode.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Verify(r => r.Delete(episode.TvShowId, episode.Id), Times.Once);
    }
}
