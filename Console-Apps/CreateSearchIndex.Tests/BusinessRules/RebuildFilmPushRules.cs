using Azure.Search.Documents;
using CreateSearchIndex;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

namespace CreateSearchIndex.Tests;

/// <summary>
/// #1408: a teardown with <c>--run-indexer</c> pushes every film into the recreated index in the same run.
/// Synthetic index names.
/// </summary>
public class RebuildFilmPushRules
{
    private const string ConfiguredIndex = "synthetic-index";

    private readonly AutoMocker _mocker = new();

    public RebuildFilmPushRules()
    {
        // AutoMocker builds Azure SDK clients through constructors that need an endpoint; Mock.Of uses the protected
        // parameterless one, and the mock is registered with AutoMocker so setups still go through GetMock<T>().
        _mocker.Use(Mock.Get(Mock.Of<SearchClient>()));
        _mocker.GetMock<SearchClient>().SetupGet(x => x.IndexName).Returns(ConfiguredIndex);
        _mocker.GetMock<IFilmSearchPusher>()
            .Setup(x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(succeeded: true));
    }

    private RebuildFilmPush Sut => _mocker.CreateInstance<RebuildFilmPush>();

    private static FilmSearchPushResult Result(bool succeeded) =>
        new(succeeded, FullPush: true, DryRun: false, PreviousWatermark: null, NewWatermark: succeeded ? 2 : null,
            FilmsRead: 2, ToPush: 2, ToDelete: 0, Failed: succeeded ? 0 : 1, WatermarkSaved: succeeded);

    [Fact(DisplayName =
        "When the configured index is rebuilt, then every film is pushed with a full push, because the teardown " +
        "deleted every film document and films have no pull indexer.")]
    public async Task rebuilt_configured_index_gets_a_full_film_push()
    {
        // Arrange
        var sut = Sut;

        // Act
        var pushed = await sut.PushAfterRebuild(ConfiguredIndex, CancellationToken.None);

        // Assert
        pushed.Should().BeTrue();
        _mocker.GetMock<IFilmSearchPusher>().Verify(
            x => x.Push(It.Is<FilmSearchPushRequest>(r => r.Full && !r.DryRun && r.FilmId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When another index name is rebuilt, then no film is pushed, because the pusher writes to the configured " +
        "index and the films would land in the wrong one.")]
    public async Task other_index_is_not_pushed_to()
    {
        // Arrange
        var sut = Sut;

        // Act
        var pushed = await sut.PushAfterRebuild("synthetic-other-index", CancellationToken.None);

        // Assert
        pushed.Should().BeFalse();
        _mocker.GetMock<IFilmSearchPusher>().Verify(
            x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the film push is incomplete, then the rebuild reports it without throwing, because the index and its " +
        "indexers are already rebuilt and the scheduled refresh pushes the films again.")]
    public async Task incomplete_push_is_reported_not_thrown()
    {
        // Arrange
        _mocker.GetMock<IFilmSearchPusher>()
            .Setup(x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(succeeded: false));
        var sut = Sut;

        // Act
        var pushed = await sut.PushAfterRebuild(ConfiguredIndex, CancellationToken.None);

        // Assert
        pushed.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the film push throws, then the rebuild reports it without throwing, because the index and its " +
        "indexers are already rebuilt and the scheduled refresh pushes the films again.")]
    public async Task throwing_push_is_reported_not_thrown()
    {
        // Arrange
        _mocker.GetMock<IFilmSearchPusher>()
            .Setup(x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic failure"));
        var sut = Sut;

        // Act
        var act = () => sut.PushAfterRebuild(ConfiguredIndex, CancellationToken.None);

        // Assert
        (await act.Should().NotThrowAsync()).Which.Should().BeFalse();
    }
}
