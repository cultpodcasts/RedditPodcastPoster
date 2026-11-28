using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Models;
using Api.Services.Episodes;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using EpisodeTvShowTrailerAttachRequest = Api.Models.EpisodeTvShowTrailerAttachRequest;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// RPP #1412: the TV show trailer-attach handler maps the service outcome to the command response (docs/tv-show-trailers.md).
/// </summary>
public class PostEpisodeTvShowTrailerAttachHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;

    public PostEpisodeTvShowTrailerAttachHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.Conflict(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
    }

    private EpisodeTvShowTrailerAttachCommand Command() => new(
        _fixture.CreateGuid(),
        _fixture.CreateGuid(),
        new EpisodeTvShowTrailerAttachRequest { TvShowId = _fixture.CreateGuid() });

    private Task Handle(EpisodeTvShowTrailerAttachResult result)
    {
        _mocker.GetMock<IEpisodeTvShowTrailerAttachService>()
            .Setup(s => s.AttachAsync(It.IsAny<EpisodeTvShowTrailerAttachCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return _mocker.CreateInstance<PostEpisodeTvShowTrailerAttachHandler>()
            .Handle(_ctx.Object, Command(), CancellationToken.None);
    }

    [Fact(DisplayName =
        "PostEpisodeTvShowTrailerAttachHandler: when the attach is accepted, Handle responds 202 with the show id, name and showCreated, and no read model, because a command returns an outcome.")]
    public async Task accepted_returns_202_with_outcome()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var name = _fixture.CreateTitle();

        // Act
        await Handle(new EpisodeTvShowTrailerAttachResult(
            EpisodeTvShowTrailerAttachStatus.Accepted, showId, name, ShowCreated: true));

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<EpisodeTvShowTrailerAttachResponse>(r =>
                r.TvShowId == showId && r.TvShowName == name && r.ShowCreated && !r.FailureIndexing),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostEpisodeTvShowTrailerAttachHandler: when AttachAsync returns Conflict because another TV show holds the trailer, Handle responds 409 with that show id, because one episode cannot be a trailer of two shows.")]
    public async Task conflict_returns_409_with_holding_show_id()
    {
        // Arrange
        var holdingShowId = _fixture.CreateGuid();
        var error = _fixture.CreateTitle();

        // Act
        await Handle(new EpisodeTvShowTrailerAttachResult(
            EpisodeTvShowTrailerAttachStatus.Conflict, holdingShowId, Error: error));

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<EpisodeTvShowTrailerAttachResponse>(r => r.TvShowId == holdingShowId && r.Error == error),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostEpisodeTvShowTrailerAttachHandler: when AttachAsync fails during search clean-up, Handle responds 500 with failureIndexing true, so the client prompts a re-send.")]
    public async Task failed_returns_500_with_failure_indexing()
    {
        // Arrange
        var showId = _fixture.CreateGuid();

        // Act
        await Handle(new EpisodeTvShowTrailerAttachResult(
            EpisodeTvShowTrailerAttachStatus.Failed, showId, FailureIndexing: true));

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeTvShowTrailerAttachResponse>(r => r.TvShowId == showId && r.FailureIndexing),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

