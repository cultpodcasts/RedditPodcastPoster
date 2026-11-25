using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Search.Services;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1408: films have no pull indexer, so the scheduled refresh (the publisher activity) pushes them by Cosmos
/// <c>_ts</c>. A film push problem must never fail the hour, the episode indexer run or the homepage publish.
/// </summary>
public class PublisherFilmPushRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<string> _calls = [];

    public PublisherFilmPushRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions()));
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(_mocker.GetMock<IMemoryProbeScope>().Object);
        _mocker.GetMock<FunctionContext>()
            .SetupGet(x => x.InvocationId)
            .Returns(_fixture.Create<string>());
        var reason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>()
            .Setup(x => x.RunPublisher(out reason))
            .Returns(true);
        _mocker.GetMock<IHomepagePublisher>()
            .Setup(x => x.PublishHomepage())
            .Callback(() => _calls.Add("homepage"))
            .ReturnsAsync(new PublishHomepageResult(true));
        _mocker.GetMock<ID1SearchChangeFeed>()
            .Setup(x => x.Drip(It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("d1"))
            .ReturnsAsync(new D1SearchChangeFeedResult(true, true, 0, 0, 0, false));
        _mocker.GetMock<IFilmSearchPusher>()
            .Setup(x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("films"))
            .ReturnsAsync(Result(succeeded: true));
    }

    private static FilmSearchPushResult Result(bool succeeded) =>
        new(succeeded, FullPush: false, DryRun: false, PreviousWatermark: 1, NewWatermark: succeeded ? 2 : 1,
            FilmsRead: 1, ToPush: 1, ToDelete: 0, Failed: succeeded ? 0 : 1, WatermarkSaved: succeeded);

    private Task<IndexerContext> Run() =>
        _mocker.CreateInstance<global::Indexer.Activities.Publisher>().Run(
            new IndexerContext(PublisherOperationId: _fixture.CreateGuid()),
            _mocker.GetMock<FunctionContext>().Object);

    [Fact(DisplayName =
        "When the publisher activity runs, then films get an incremental push after the homepage and after the D1 " +
        "drip, because films have no pull indexer and the scheduled refresh keeps them current, while a slow Azure " +
        "film push must never delay D1.")]
    public async Task films_are_pushed_incrementally_after_the_homepage_and_d1()
    {
        // Arrange

        // Act
        var result = await Run();

        // Assert
        result.Success.Should().BeTrue();
        _calls.Should().Equal("homepage", "d1", "films");
        _mocker.GetMock<IFilmSearchPusher>().Verify(
            x => x.Push(It.Is<FilmSearchPushRequest>(r => !r.Full && !r.DryRun && r.FilmId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When the film push throws, then the publisher activity still succeeds and the D1 drip still runs, because a " +
        "film push failure must never fail the hour.")]
    public async Task a_throwing_film_push_never_fails_the_activity()
    {
        // Arrange
        _mocker.GetMock<IFilmSearchPusher>()
            .Setup(x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));

        // Act
        var result = await Run();

        // Assert
        result.Success.Should().BeTrue();
        _calls.Should().Equal("homepage", "d1");
    }

    [Fact(DisplayName =
        "When the film push reports an incomplete run, then the publisher activity still succeeds, because the " +
        "pusher keeps its watermark and the next refresh retries.")]
    public async Task an_incomplete_film_push_never_fails_the_activity()
    {
        // Arrange
        _mocker.GetMock<IFilmSearchPusher>()
            .Setup(x => x.Push(It.IsAny<FilmSearchPushRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(succeeded: false));

        // Act
        var result = await Run();

        // Assert
        result.Success.Should().BeTrue();
        _mocker.GetMock<ID1SearchChangeFeed>().Verify(x => x.Drip(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When the episode indexer run throws, then the D1 drip and then the film push still run and the activity still " +
        "fails, because neither depends on the episode indexer or the homepage.")]
    public async Task films_are_pushed_when_the_episode_indexer_fails()
    {
        // Arrange
        _mocker.GetMock<ISearchIndexerService>()
            .Setup(x => x.RunIndexer())
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));

        // Act
        Func<Task> act = Run;

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _calls.Should().Equal("d1", "films");
    }
}
