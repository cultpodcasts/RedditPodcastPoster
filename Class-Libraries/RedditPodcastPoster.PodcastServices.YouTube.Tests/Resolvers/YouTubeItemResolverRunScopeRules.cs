using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Resolvers;

/// <summary>
/// A podcast with a negative YouTube publishing delay makes the item resolver rebuild its indexing policy with an
/// earlier released-since. Before #1073 that rebuild also dropped the per-podcast budget token and stage reporter, so
/// the playlist walk ran uncancellable and untraced. The budget now travels on the ambient
/// <see cref="PodcastRunScope"/>, so the walk is cancelled with the podcast's update (a deliberate behaviour change).
/// </summary>
public class YouTubeItemResolverRunScopeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeItemResolver> _logger = new();
    private readonly CancellationTokenSource _budget = new();
    private readonly Podcast _podcast;
    private PodcastRunScope? _playlistWalkScope;
    private IndexingContext? _playlistWalkContext;

    public YouTubeItemResolverRunScopeRules()
    {
        _podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePublicationOffset = DomainTestFixture.DefaultNegativeYouTubePublishingDelayTicks;
        });
        _mocker.Use<ILogger<YouTubeItemResolver>>(_logger);
        _mocker.GetMock<ICachedTolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .Returns((YouTubePlaylistId _, IndexingContext context, bool _, PlaylistOrder? _) =>
            {
                // The playlist walk reads the budget the way YouTubePlaylistService does: from the run scope.
                _playlistWalkScope = PodcastRunScope.Current;
                _playlistWalkContext = context;
                _playlistWalkScope.CancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new GetPlaylistVideoSnippetsResponse([]));
            });
    }

    [Fact(DisplayName =
        "When a podcast has a negative YouTube publishing delay, the playlist walk still sees the podcast's run scope " +
        "even though the resolver rebuilds its indexing policy, because the per-podcast budget and stage reporter " +
        "must reach the walk (#1073; before, the rebuilt policy dropped them).")]
    public async Task negative_publishing_delay_walk_keeps_the_run_scope()
    {
        // Arrange
        var batchContext = new IndexingContext(DomainTestFixture.UtcDateDaysAgo(7));
        var runScope = new PodcastRunScope(_budget.Token);
        var sut = _mocker.CreateInstance<YouTubeItemResolver>();

        // Act
        using (PodcastRunScope.Enter(runScope))
        {
            await sut.FindEpisode(CreateRequest(), batchContext);
        }

        // Assert
        _playlistWalkScope.Should().BeSameAs(runScope);
        _playlistWalkContext.Should().NotBeSameAs(batchContext, "the negative delay rebuilds the policy");
    }

    [Fact(DisplayName =
        "When a podcast has a negative YouTube publishing delay and its update budget is already spent, the playlist " +
        "walk's cancellation propagates out of the resolver and nothing is logged as an error, because a budget cut " +
        "is reported by the indexer loop and must not be treated as a YouTube failure.")]
    public async Task negative_publishing_delay_walk_is_cancelled_by_the_budget()
    {
        // Arrange
        await _budget.CancelAsync();
        var sut = _mocker.CreateInstance<YouTubeItemResolver>();

        // Act
        Task<FindEpisodeResponse?> find;
        using (PodcastRunScope.Enter(new PodcastRunScope(_budget.Token)))
        {
            find = sut.FindEpisode(CreateRequest(), new IndexingContext(DomainTestFixture.UtcDateDaysAgo(7)));
        }

        // Assert
        await find.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error);
    }

    private EnrichmentRequest CreateRequest() =>
        new(_podcast, [], _fixture.CreateEpisode());
}
