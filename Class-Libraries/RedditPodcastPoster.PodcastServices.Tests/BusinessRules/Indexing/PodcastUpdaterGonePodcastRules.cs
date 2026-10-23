using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// CatalogueMigrate deletes the source podcast after moving it. A pass that already read the podcast must not
/// write that row back or save episodes under it.
/// </summary>
public class PodcastUpdaterGonePodcastRules
{
    private static readonly DateTime EpisodeRelease = DomainTestFixture.UtcDateDaysAgo(30);
    private readonly DomainTestFixture _fixture = new();
    private readonly SaveCallRecorder _recorder = new();
    private readonly RecordingLogger<PodcastUpdater> _logger = new();
    private readonly PodcastUpdaterTestHarness _harness;
    private readonly Podcast _podcast;

    public PodcastUpdaterGonePodcastRules()
    {
        _harness = new PodcastUpdaterTestHarness(_recorder, _logger);
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

    public static TheoryData<MovedParentKind> MovedParentKinds() =>
        new()
        {
            MovedParentKind.TvShow,
            MovedParentKind.NewsOrganisation
        };

    [Fact(DisplayName =
        "When the podcast row is deleted before save-episodes, no episodes are saved and the podcast is not " +
        "re-created, and the warning names IndexerPodcastGone with the podcast id and stage save-episodes, because " +
        "a pass that read the row before CatalogueMigrate deleted it must not write it back.")]
    public async Task deleted_before_save_episodes_writes_nothing()
    {
        // Arrange
        DeletePodcastWhenEnrichmentRuns();

        // Act
        await _harness.Updater.Update(_podcast, false, PodcastUpdaterTestHarness.DefaultIndexingContext());

        // Assert
        _recorder.EpisodeCalls.Should().BeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
        (await _harness.PodcastRepository.GetPodcast(_podcast.Id)).Should().BeNull();
        var warning = SingleGoneWarning();
        warning.Message.Should().Contain($"podcast-id={_podcast.Id}");
        warning.Message.Should().Contain("stage=save-episodes");
        warning.Message.Should().NotContain("moved-to=");
    }

    [Fact(DisplayName =
        "When the podcast row disappears after the save-episodes point-read and before save-podcast, the podcast is " +
        "not re-created, and the warning names IndexerPodcastGone with stage save-podcast, because that upsert would " +
        "restore a row CatalogueMigrate already removed.")]
    public async Task deleted_before_save_podcast_does_not_recreate_the_podcast()
    {
        // Arrange
        _harness.EpisodeRepository.OnSaving = () =>
            _harness.PodcastRepository.Delete(_podcast.Id).GetAwaiter().GetResult();

        // Act
        await _harness.Updater.Update(_podcast, false, PodcastUpdaterTestHarness.DefaultIndexingContext());

        // Assert
        _recorder.EpisodeCalls.Should().NotBeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
        (await _harness.PodcastRepository.GetPodcast(_podcast.Id)).Should().BeNull();
        var warning = SingleGoneWarning();
        warning.Message.Should().Contain($"podcast-id={_podcast.Id}");
        warning.Message.Should().Contain("stage=save-podcast");
    }

    [Fact(DisplayName =
        "When the podcast row is still present at both writes, the discovered episode and the podcast are saved and " +
        "no IndexerPodcastGone warning is logged, because the point-read only skips a row that is already gone.")]
    public async Task podcast_still_present_is_saved()
    {
        // Arrange
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext();

        // Act
        await _harness.Updater.Update(_podcast, false, context);

        // Assert
        _recorder.EpisodeCalls.Should().NotBeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().NotBeEmpty();
        (await _harness.PodcastRepository.GetPodcast(_podcast.Id)).Should().NotBeNull();
        _logger.Entries.Should().NotContain(entry =>
            entry.Message.StartsWith(PodcastUpdater.PodcastGoneMessagePrefix));
        _harness.TvShows.Verify(x => x.GetTvShow(It.IsAny<Guid>()), Times.Never);
        _harness.NewsOrganisations.Verify(x => x.GetNewsOrganisation(It.IsAny<Guid>()), Times.Never);
    }

    [Theory(DisplayName =
        "When a podcast deleted before save-episodes now exists as a TvShow or a NewsOrganisation with the same id, " +
        "no episodes or podcast are written, and the warning names IndexerPodcastGone, the stage, and where it moved.")]
    [MemberData(nameof(MovedParentKinds))]
    public async Task deleted_podcast_that_moved_names_the_destination(MovedParentKind kind)
    {
        // Arrange
        var name = _fixture.CreateTitle();
        if (kind == MovedParentKind.TvShow)
        {
            var show = new TvShow(name) { Id = _podcast.Id };
            _harness.TvShows.Setup(x => x.GetTvShow(_podcast.Id)).ReturnsAsync(show);
        }
        else
        {
            var organisation = new NewsOrganisation(name) { Id = _podcast.Id };
            _harness.NewsOrganisations
                .Setup(x => x.GetNewsOrganisation(_podcast.Id))
                .ReturnsAsync(organisation);
        }

        DeletePodcastWhenEnrichmentRuns();

        // Act
        await _harness.Updater.Update(_podcast, false, PodcastUpdaterTestHarness.DefaultIndexingContext());

        // Assert
        _recorder.EpisodeCalls.Should().BeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
        var warning = SingleGoneWarning();
        warning.Message.Should().Contain($"podcast-id={_podcast.Id}");
        warning.Message.Should().Contain("stage=save-episodes");
        warning.Message.Should().Contain($"moved-to={kind}");
    }

    private void DeletePodcastWhenEnrichmentRuns()
    {
        _harness.EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback(() => _harness.PodcastRepository.Delete(_podcast.Id).GetAwaiter().GetResult())
            .ReturnsAsync(new EnrichmentResults([]));
    }

    private (LogLevel Level, string Message) SingleGoneWarning()
    {
        var warnings = _logger.Entries
            .Where(entry => entry.Level == LogLevel.Warning &&
                            entry.Message.StartsWith(PodcastUpdater.PodcastGoneMessagePrefix))
            .ToList();
        warnings.Should().ContainSingle();
        return warnings[0];
    }

    public enum MovedParentKind
    {
        TvShow,
        NewsOrganisation
    }
}
