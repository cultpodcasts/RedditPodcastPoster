using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The indexer's per-podcast budget cancels a podcast's update. Cancellation must never leave a half-saved podcast or
/// let an abandoned update (still running in the background) write after it was cut off.
/// </summary>
public class PodcastUpdaterCancellationRules
{
    private static readonly DateTime EpisodeRelease = DomainTestFixture.UtcDateDaysAgo(30);
    private readonly DomainTestFixture _fixture = new();
    private readonly SaveCallRecorder _recorder = new();
    private readonly PodcastUpdaterTestHarness _harness;
    private readonly Podcast _podcast;
    private readonly CancellationTokenSource _budget = new();

    public PodcastUpdaterCancellationRules()
    {
        _harness = new PodcastUpdaterTestHarness(_recorder);
        _podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(_podcast);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration())
            .WithDescription(_fixture.CreateTitle()));
        _harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(_podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);
    }

    [Fact(DisplayName =
        "When the podcast's budget is cancelled during enrichment, the update throws before any episode or podcast is " +
        "saved, because a cut-off update must not half-save; the next pass repeats the work.")]
    public async Task cancellation_during_enrichment_saves_nothing()
    {
        // Arrange
        _harness.EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback(() => _budget.Cancel())
            .ReturnsAsync(new EnrichmentResults([]));
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext() with { CancellationToken = _budget.Token };

        // Act
        var act = () => _harness.Updater.Update(_podcast, false, context);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _recorder.EpisodeCalls.Should().BeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an update ignored its cancelled token in a call ahead of the saves and that call finally returns, no " +
        "Cosmos write starts, because an update the indexer already abandoned must not write alongside the next podcast.")]
    public async Task abandoned_update_returning_late_does_not_write()
    {
        // Arrange
        var release = new TaskCompletionSource<EnrichmentResults>();
        _harness.EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Returns(release.Task);
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext() with { CancellationToken = _budget.Token };
        var update = _harness.Updater.Update(_podcast, false, context);

        // Act
        _budget.Cancel();
        release.SetResult(new EnrichmentResults([]));
        var act = () => update;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _recorder.EpisodeCalls.Should().BeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the podcast's budget is cancelled during the save-podcast point-read, the update throws and does not " +
        "save the podcast, because a Cosmos read that ignores the token must not be followed by an upsert the indexer " +
        "already abandoned.")]
    public async Task cancellation_during_save_podcast_point_read_does_not_save_the_podcast()
    {
        // Arrange
        var reads = 0;
        _harness.PodcastRepository.OnReading = () =>
        {
            reads++;
            if (reads >= 2)
            {
                _budget.Cancel();
            }
        };
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext() with { CancellationToken = _budget.Token };

        // Act
        var act = () => _harness.Updater.Update(_podcast, false, context);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Without a cancelled token the same update saves the discovered episode and the podcast, so the cancellation " +
        "rules above are not passing only because nothing would be saved anyway.")]
    public async Task uncancelled_update_saves()
    {
        // Arrange
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext() with { CancellationToken = _budget.Token };

        // Act
        await _harness.Updater.Update(_podcast, false, context);

        // Assert
        _recorder.EpisodeCalls.Should().NotBeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().NotBeEmpty();
    }
}
