using Api.Dtos;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Models;
using Api.Services.Episodes;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using EpisodeNewsMoveRequest = Api.Models.EpisodeNewsMoveRequest;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>#1388: status mapping of the single and batch move-to-news routes.</summary>
public class EpisodeNewsMoveHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IHandlerContext> _ctx;
    private EpisodeNewsMoveResult _result = new(EpisodeNewsMoveStatus.Accepted);

    public EpisodeNewsMoveHandlerRules()
    {
        _ctx = _mocker.GetMock<IHandlerContext>();
        _ctx.Setup(c => c.InternalError(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Accepted(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        _ctx.Setup(c => c.Conflict(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object _, CancellationToken _) => null!);
        var service = _mocker.GetMock<IEpisodeNewsMoveService>();
        service
            .Setup(s => s.MoveAsync(It.IsAny<EpisodeNewsMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
        service
            .Setup(s => s.MoveManyAsync(It.IsAny<PodcastNewsMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _result);
    }

    [Fact(DisplayName =
        "PostEpisodeNewsMoveHandler: when MoveAsync returns Failed with failureIndexing, Handle responds 500 with the move DTO including failureIndexing, the same contract as the TV and film moves.")]
    public async Task single_failed_returns_dto_with_failure_indexing()
    {
        // Arrange
        var organisationId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        _result = new EpisodeNewsMoveResult(
            EpisodeNewsMoveStatus.Failed, organisationId, _fixture.CreateTitle(), FailureIndexing: true)
        {
            EpisodeId = episodeId
        };
        var command = new EpisodeNewsMoveCommand(
            _fixture.CreateGuid(), episodeId, new EpisodeNewsMoveRequest { NewsOrganisationId = organisationId });
        var sut = _mocker.CreateInstance<PostEpisodeNewsMoveHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.InternalError(
            It.Is<EpisodeNewsMoveResponse>(r =>
                r.FailureIndexing && r.NewsOrganisationId == organisationId && r.EpisodeId == episodeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostPodcastNewsMoveHandler: an accepted batch responds 202 with the moved episode ids (a command outcome, not the read model).")]
    public async Task batch_accepted_returns_202_with_moved_ids()
    {
        // Arrange
        var moved = _fixture.CreateGuid();
        _result = new EpisodeNewsMoveResult(
            EpisodeNewsMoveStatus.Accepted, _fixture.CreateGuid(), _fixture.CreateTitle(), MovedEpisodeIds: [moved]);
        var command = new PodcastNewsMoveCommand(_fixture.CreateGuid(), [moved], new EpisodeNewsMoveRequest());
        var sut = _mocker.CreateInstance<PostPodcastNewsMoveHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Accepted(
            It.Is<EpisodeNewsMoveResponse>(r => r.MovedEpisodeIds.Single() == moved),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "PostPodcastNewsMoveHandler: a refused batch with conflicting ids responds 409 with the conflict ids, so the curator sees which ids blocked it.")]
    public async Task batch_conflict_returns_409_with_ids()
    {
        // Arrange
        var conflict = _fixture.CreateGuid();
        _result = new EpisodeNewsMoveResult(EpisodeNewsMoveStatus.Conflict, ConflictEpisodeIds: [conflict]);
        var command = new PodcastNewsMoveCommand(_fixture.CreateGuid(), [conflict], new EpisodeNewsMoveRequest());
        var sut = _mocker.CreateInstance<PostPodcastNewsMoveHandler>();

        // Act
        await sut.Handle(_ctx.Object, command, CancellationToken.None);

        // Assert
        _ctx.Verify(x => x.Conflict(
            It.Is<EpisodeNewsMoveResponse>(r => r.ConflictEpisodeIds.Single() == conflict),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
