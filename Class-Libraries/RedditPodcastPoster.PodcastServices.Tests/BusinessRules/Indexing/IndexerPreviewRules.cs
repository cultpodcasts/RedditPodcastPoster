using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The backlog index confirmation (#1433) previews an on-demand index by podcast name. The preview resolves the podcast
/// the way the real index does, answers NotPerformed where the real index would add nothing, never runs the real
/// update, and runs the dry run under the same on-demand budget.
/// </summary>
public class IndexerPreviewRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(60);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RecordingLogger<Indexer> _logger = new();
    private readonly PodcastUpdateBudgetOptions _budget = new();
    private readonly InMemoryPodcastRepository _podcastRepository = new();
    private readonly Podcast _podcast;
    private IndexPreviewResult _preview;
    private IndexingContext? _previewContext;
    private Func<Task<IndexPreviewResult>>? _previewRun;

    public IndexerPreviewRules()
    {
        _podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _podcast.IndexAllEpisodes = true;
        _podcastRepository.Seed(_podcast);
        _preview = new IndexPreviewResult(_fixture.Create<int>() % 50 + 2, 1);

        _mocker.Use<IPodcastRepository>(_podcastRepository);
        _mocker.Use<IEpisodeRepository>(new InMemoryEpisodeRepository());
        _mocker.Use<IParentIndexingLockStore>(new InMemoryParentIndexingLockStore());
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use<ILogger<Indexer>>(_logger);
        _mocker.Use<IOptions<PodcastUpdateBudgetOptions>>(Options.Create(_budget));

        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Preview(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()))
            .Callback<Podcast, IndexingContext>((_, context) => _previewContext = context)
            .Returns(() => _previewRun?.Invoke() ?? Task.FromResult(_preview));
    }

    [Fact(DisplayName =
        "Previewing an indexable podcast by name answers Performed with the updater's counts and never runs the real update, because a preview must not ingest anything.")]
    public async Task preview_answers_the_updaters_counts_without_running_the_update()
    {
        // Arrange
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await sut.Preview(_podcast.Name, new IndexingContext(ReleasedSince))
            .WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        response.Should().Be(new IndexPreviewResponse(
            IndexStatus.Performed,
            _preview.NewEpisodeCount,
            _preview.IgnoredEpisodeCount));
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Update(It.IsAny<Podcast>(), It.IsAny<bool>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Previewing a podcast name that matches no podcast answers NotFound, because there is nothing to count.")]
    public async Task preview_of_an_unknown_name_is_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await sut.Preview(_fixture.CreateTitle(), new IndexingContext(ReleasedSince));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.NotFound);
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Preview(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Previewing a removed podcast answers NotPerformed without running the preview, because the real index does not run on a removed podcast.")]
    public async Task preview_of_a_removed_podcast_is_not_performed()
    {
        // Arrange
        _podcast.Removed = true;
        _podcastRepository.Seed(_podcast);
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await sut.Preview(_podcast.Name, new IndexingContext(ReleasedSince));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.NotPerformed);
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Preview(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Previewing a podcast that neither indexes all episodes nor has an include-title regex answers NotPerformed without running the preview, because an on-demand index of it only enriches stored episodes.")]
    public async Task preview_of_a_podcast_that_is_not_auto_indexed_is_not_performed()
    {
        // Arrange
        _podcast.IndexAllEpisodes = false;
        _podcast.EpisodeIncludeTitleRegex = string.Empty;
        _podcastRepository.Seed(_podcast);
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await sut.Preview(_podcast.Name, new IndexingContext(ReleasedSince));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.NotPerformed);
        _mocker.GetMock<IPodcastUpdater>().Verify(
            x => x.Preview(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "Previewing a podcast skips short episodes unless BypassShortEpisodeChecking is explicitly true, because the count must match what the real index would ingest.")]
    [InlineData(null, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task preview_derives_skip_short_episodes_from_the_bypass_flag(
        bool? bypassShortEpisodeChecking,
        bool expectedSkipShortEpisodes)
    {
        // Arrange
        _podcast.BypassShortEpisodeChecking = bypassShortEpisodeChecking;
        _podcastRepository.Seed(_podcast);
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        await sut.Preview(_podcast.Name, new IndexingContext(ReleasedSince, SkipShortEpisodes: false));

        // Assert
        _previewContext.Should().NotBeNull();
        _previewContext!.SkipShortEpisodes.Should().Be(expectedSkipShortEpisodes);
    }

    [Fact(DisplayName =
        "When the preview's run scope token is cancelled by the on-demand budget, the preview stops and the answer is TimedOut, because a dry run must not outlive the on-demand budget.")]
    public async Task preview_that_exceeds_the_on_demand_budget_is_timed_out()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _previewRun = async () =>
        {
            var token = PodcastRunScope.Current.CancellationToken;
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return _preview;
        };
        var sut = _mocker.CreateInstance<Indexer>();

        // Act
        var run = sut.Preview(_podcast.Name, new IndexingContext(ReleasedSince));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _time.Advance(_budget.OnDemandBudget);
        var response = await run.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.TimedOut);
        _logger.Entries.Should().ContainSingle(entry =>
            entry.Message.StartsWith(Indexer.BudgetExceededMessagePrefix) &&
            entry.Message.Contains("dry-run='true'"));
    }
}
