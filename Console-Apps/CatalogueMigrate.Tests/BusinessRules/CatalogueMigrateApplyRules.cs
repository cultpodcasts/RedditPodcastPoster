using CatalogueMigrate;
using CatalogueMigrate.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Xunit;

namespace CatalogueMigrate.Tests.BusinessRules;

/// <summary>
/// Apply / rollback rules against a synthetic in-memory store. No Cosmos, no Azure Search.
/// </summary>
public class CatalogueMigrateApplyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryCatalogueMigrateDestination _destination = new();
    private readonly InMemoryCatalogueMigrateEvidenceStore _evidence = new();
    private readonly FixedTimeProvider _clock = new(DomainTestFixture.UtcToday);
    private readonly AutoMocker _mocker = new();

    public CatalogueMigrateApplyRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<ICatalogueMigrateDestination>(_destination);
        _mocker.Use<ICatalogueMigrateDestinationReader>(_destination);
        _mocker.Use<ICatalogueMigrateEvidenceStore>(_evidence);
        _mocker.Use<TimeProvider>(_clock);
        _mocker.Use(NullLogger<CatalogueMigrateProcessor>.Instance);
        _mocker.Use(NullLogger<CatalogueMigrateApplier>.Instance);
        _mocker.Use(NullLogger<CatalogueMigrateRollbackPreviewer>.Instance);
        _mocker.Use(_mocker.CreateInstance<CatalogueMigrateRollbackPreviewer>());
        _mocker.Use<Func<CatalogueMigrateApplier>>(() => _mocker.CreateInstance<CatalogueMigrateApplier>());
    }

    [Fact(DisplayName =
        "Catalogue migrate: when --apply has a consent phrase for a different kind, then the tool exits 2 and writes nothing, " +
        "because consent for one kind must not be replayed for another.")]
    public async Task consent_for_another_kind_is_refused()
    {
        // Arrange
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(
            SubmitClassification.NewsReport,
            consentKind: SubmitClassification.TvShowEpisode));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitApplyRefused);
        result.PlannedCount.Should().Be(0);
        _destination.NewsOrganisations.Should().BeEmpty();
        _evidence.Files.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when News is applied with consent for an S-008 publisher, then the organisation and every report keep " +
        "their ids, removed episodes stay removed, search swaps only active rows to NewsReport, and the podcast rows are deleted, " +
        "because migrate moves rather than copies (S-006).")]
    public async Task news_apply_moves_allowlisted_publisher_keeping_ids()
    {
        // Arrange
        var (podcast, episodes) = SeedAllowlistedNewsPodcast(activeEpisodes: 2, removedEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        result.AppliedCount.Should().Be(1);
        _destination.NewsOrganisations.Keys.Should().Equal(podcast.Id);
        _destination.NewsReports.Keys.Select(key => key.Id).Should().BeEquivalentTo(episodes.Select(e => e.Id));
        _destination.NewsReports.Values.Count(report => report.Removed).Should().Be(1);
        _destination.Search.Values.Should().HaveCount(2)
            .And.OnlyContain(document => document.ContentKind == SearchContentKind.NewsReport);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        (await _episodes.GetByPodcastId(podcast.Id).ToListAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: the begin evidence record holds the full source snapshot and is written before any destination write, " +
        "because a batch must be restorable from its own evidence.")]
    public async Task evidence_begin_is_first_and_holds_snapshot()
    {
        // Arrange
        var (podcast, episodes) = SeedAllowlistedNewsPodcast(activeEpisodes: 2);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        var records = _evidence.Records(result.Evidence.Single());
        records.Select(record => record.Event).Should().Equal(
            CatalogueMigrateEvidenceEvent.Begin,
            CatalogueMigrateEvidenceEvent.DestWritten,
            CatalogueMigrateEvidenceEvent.Verified,
            CatalogueMigrateEvidenceEvent.SearchSwapped,
            CatalogueMigrateEvidenceEvent.SourceDeleted,
            CatalogueMigrateEvidenceEvent.Completed);
        records[0].BeforePodcast!.Id.Should().Be(podcast.Id);
        records[0].BeforePodcast!.Name.Should().Be(podcast.Name);
        records[0].BeforeEpisodes!.Select(e => e.Id).Should().BeEquivalentTo(episodes.Select(e => e.Id));
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a News candidate's publisher is not on S-008, then apply leaves it in Podcasts and the plan " +
        "line says why, because News apply is allowlist-only (S-008).")]
    public async Task news_apply_refuses_publisher_off_the_allowlist()
    {
        // Arrange
        var podcast = SeedHeuristicNewsPodcastOffAllowlist();
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.NotOnAllowlist);
        _destination.NewsOrganisations.Should().BeEmpty();
        _evidence.Files.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: dry-run plan lines are identical to the apply plan lines for the same corpus, " +
        "because the dry-run is the reviewed apply plan.")]
    public async Task dry_run_plan_equals_apply_plan()
    {
        // Arrange
        SeedAllowlistedNewsPodcast(activeEpisodes: 2, removedEpisodes: 1);
        SeedHeuristicNewsPodcastOffAllowlist();
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var dryRun = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });
        var apply = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        dryRun.PlanLines.Should().HaveCount(2);
        apply.PlanLines.Should().BeEquivalentTo(dryRun.PlanLines, options => options.WithStrictOrdering());
        dryRun.SearchDocumentCount.Should().Be(apply.SearchDocumentCount);
        apply.AppliedCount.Should().Be(1);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when the search swap fails, then the run stops with exit 3 and keeps the podcast rows, and a re-run " +
        "completes the move without duplicating playables, because batches are idempotent and resumable.")]
    public async Task search_failure_stops_then_rerun_resumes()
    {
        // Arrange
        var (podcast, episodes) = SeedAllowlistedNewsPodcast(activeEpisodes: 2);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();
        _destination.FailSearch = true;

        // Act
        var failed = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));
        var keptAfterFailure = await _podcasts.GetPodcast(podcast.Id);
        _destination.FailSearch = false;
        _clock.Now = _clock.Now.AddMinutes(5);
        var resumed = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        failed.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitFailed);
        keptAfterFailure.Should().NotBeNull();
        _evidence.Records(failed.Evidence.Single()).Last().Event.Should().Be(CatalogueMigrateEvidenceEvent.Failed);
        resumed.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        resumed.AppliedCount.Should().Be(1);
        _destination.NewsReports.Should().HaveCount(episodes.Count);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: --batch-size splits evidence into one file per batch and --limit stops early, so a re-run continues " +
        "with the podcasts not yet moved.")]
    public async Task batches_and_limit_resume()
    {
        // Arrange
        SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var first = await sut.Run(ApplyRequest(SubmitClassification.NewsReport, batchSize: 1, limit: 2));
        _clock.Now = _clock.Now.AddMinutes(5);
        var second = await sut.Run(ApplyRequest(SubmitClassification.NewsReport, batchSize: 1));

        // Assert
        first.AppliedCount.Should().Be(2);
        first.Evidence.Should().HaveCount(2);
        second.AppliedCount.Should().Be(1);
        _destination.NewsOrganisations.Should().HaveCount(3);
        (await _podcasts.Count()).Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when the podcast id already has a NewsOrganisation, then TV apply records a conflict and writes " +
        "nothing, because one id cannot be both parent kinds.")]
    public async Task tv_apply_conflicts_with_existing_news_parent()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        await _destination.SaveNews(CatalogueParentKindMapper.ToNewsOrganisation(podcast), []);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.TvShowEpisode, podcastId: podcast.Id));

        // Assert
        result.ConflictCount.Should().Be(1);
        result.AppliedCount.Should().Be(0);
        _destination.TvShows.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: a targeted TV apply moves every episode under a TvShow with the podcast id, " +
        "because TvShow keeps the podcast GUID and each episode keeps its own (S-006).")]
    public async Task tv_apply_keeps_ids()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episodes = new[]
        {
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast),
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast)
        };
        _podcasts.Seed(podcast);
        _episodes.Seed(episodes);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.TvShowEpisode, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(1);
        _destination.TvShows.Keys.Should().Equal(podcast.Id);
        _destination.TvShowEpisodes.Keys.Select(key => key.Id).Should().BeEquivalentTo(episodes.Select(e => e.Id));
        _destination.Search.Values.Should().OnlyContain(document => document.ContentKind == SearchContentKind.TvShowEpisode);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: a targeted Film apply stores the one episode as a Film with the episode id and no parent, " +
        "because Film is a one-off (S-002).")]
    public async Task film_apply_keeps_episode_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.Film, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(1);
        _destination.Films.Keys.Should().Equal(episode.Id);
        _destination.Search[episode.Id.ToString()].ContentKind.Should().Be(SearchContentKind.Film);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a Film podcast also has removed episodes, then Film apply is blocked, " +
        "because a Film cannot hold them and deleting them would lose rows.")]
    public async Task film_apply_blocked_by_removed_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var removed = CreateRemovedYouTubeOnlyEpisode(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode, removed);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.Film, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.FilmHasOtherEpisodes);
        _destination.Films.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when an auto-indexed S-008 News podcast is applied outside the indexer window, then the news " +
        "organisation carries its indexing settings (index-all, include and title regexes, channel, expensive flag) and the " +
        "podcast is moved, because the hourly indexer keeps indexing news organisations from those settings (#1050).")]
    public async Task auto_indexed_news_podcast_moves_with_its_indexing_settings()
    {
        // Arrange
        _clock.Now = OutsideIndexerWindow();
        var podcast = SeedAutoIndexedAllowlistedNewsPodcast(p =>
        {
            p.EpisodeIncludeTitleRegex = _fixture.CreateShortTitle();
            p.TitleRegex = _fixture.CreateShortTitle();
            p.YouTubePlaylistQueryIsExpensive = true;
        });
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        result.AppliedCount.Should().Be(1);
        var indexing = _destination.NewsOrganisations[podcast.Id].Indexing!;
        indexing.IsAutoIndexed.Should().BeTrue();
        indexing.IndexAllEpisodes.Should().BeTrue();
        indexing.EpisodeIncludeTitleRegex.Should().Be(podcast.EpisodeIncludeTitleRegex);
        indexing.TitleRegex.Should().Be(podcast.TitleRegex);
        indexing.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
        indexing.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when an auto-indexed TV podcast has blank text settings, then the TV show stores them as null, " +
        "not empty strings, while the include regex and channel still carry over, because blank indexing values are absent " +
        "rather than empty in Cosmos.")]
    public async Task auto_indexed_tv_podcast_saves_blank_indexing_text_as_null()
    {
        // Arrange
        _clock.Now = OutsideIndexerWindow();
        var podcast = CreateYouTubeOnlyPodcast(_fixture.CreateShortTitle());
        podcast.EpisodeIncludeTitleRegex = _fixture.CreateShortTitle();
        podcast.TitleRegex = string.Empty;
        podcast.DescriptionRegex = " ";
        podcast.EpisodeMatchRegex = string.Empty;
        podcast.YouTubePlaylistId = string.Empty;
        _podcasts.Seed(podcast);
        _episodes.Seed(_fixture.CreateStoredEpisodeWithYouTubeOnly(podcast));
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.TvShowEpisode, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(1);
        var indexing = _destination.TvShows[podcast.Id].Indexing!;
        indexing.IsAutoIndexed.Should().BeTrue();
        indexing.EpisodeIncludeTitleRegex.Should().Be(podcast.EpisodeIncludeTitleRegex);
        indexing.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
        indexing.TitleRegex.Should().BeNull();
        indexing.DescriptionRegex.Should().BeNull();
        indexing.EpisodeMatchRegex.Should().BeNull();
        indexing.YouTubePlaylistId.Should().BeNull();
        indexing.SpotifyId.Should().BeNull();
    }

    [Theory(DisplayName =
        "Catalogue migrate: when an auto-indexed podcast would be applied while the hourly indexer may be running " +
        "(before :15 or from :55), then it is skipped with a reason and stays a podcast, because a pass that read it " +
        "before the move would save it again and re-create the deleted row.")]
    [InlineData(3)]
    [InlineData(14)]
    [InlineData(55)]
    public async Task auto_indexed_podcast_is_not_moved_while_the_indexer_may_run(int minute)
    {
        // Arrange
        _clock.Now = AtMinute(minute);
        var podcast = SeedAutoIndexedAllowlistedNewsPodcast();
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.IndexerMayBeRunning);
        _destination.NewsOrganisations.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: a podcast that is not auto-indexed is moved at any minute, because the hourly indexer never " +
        "reads or saves it.")]
    public async Task podcast_not_auto_indexed_ignores_the_indexer_window()
    {
        // Arrange
        _clock.Now = AtMinute(CatalogueMigrateIndexerWindow.SafeFromMinute - 1);
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.AppliedCount.Should().Be(1);
        _destination.NewsOrganisations.Should().ContainKey(podcast.Id);
    }

    [Fact(DisplayName =
        "Catalogue migrate: a dry run of an auto-indexed podcast inside the indexer window still plans it as apply-eligible, " +
        "because the window is checked at apply time only.")]
    public async Task dry_run_ignores_the_indexer_window()
    {
        // Arrange
        _clock.Now = AtMinute(CatalogueMigrateIndexerWindow.SafeUntilMinute);
        var podcast = SeedAutoIndexedAllowlistedNewsPodcast();
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });

        // Assert
        var line = result.PlanLines.Single();
        line.PodcastId.Should().Be(podcast.Id);
        line.Accepted.Should().BeTrue();
        line.BlockReason.Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when an episode arrives on the podcast during the move, then the source is not deleted and the " +
        "run stops with exit 3, because deleting would lose that episode and a re-run moves it.")]
    public async Task episode_arriving_mid_move_keeps_the_source()
    {
        // Arrange
        _clock.Now = OutsideIndexerWindow();
        var podcast = SeedAutoIndexedAllowlistedNewsPodcast();
        var arrived = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _destination.AfterSearchUpload = () =>
        {
            _episodes.Seed(arrived);
            return Task.CompletedTask;
        };
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitFailed);
        result.AppliedCount.Should().Be(0);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.GetByPodcastId(podcast.Id).ToListAsync()).Should().HaveCount(2);
        _evidence.Records(result.Evidence.Single()).Select(record => record.Event).Should()
            .Contain(CatalogueMigrateEvidenceEvent.Failed)
            .And.NotContain(CatalogueMigrateEvidenceEvent.SourceDeleted);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when the hourly indexer saves the podcast during the move, then the source is not deleted and " +
        "the run stops with exit 3, because the destination was written from settings the indexer has since changed.")]
    public async Task podcast_indexed_mid_move_keeps_the_source()
    {
        // Arrange
        _clock.Now = OutsideIndexerWindow();
        var podcast = SeedAutoIndexedAllowlistedNewsPodcast(p => p.LastIndexed = DomainTestFixture.UtcDaysAgo(1));
        _destination.AfterSearchUpload = async () =>
        {
            var indexed = (await _podcasts.GetPodcast(podcast.Id))!;
            indexed.LastIndexed = DomainTestFixture.UtcToday;
            await _podcasts.Save(indexed);
        };
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitFailed);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.GetByPodcastId(podcast.Id).ToListAsync()).Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a Film podcast is auto-indexed, then Film apply is blocked and the plan line says why, " +
        "because a Film has no parent to keep indexing and new episodes would stop arriving.")]
    public async Task auto_indexed_film_podcast_is_blocked()
    {
        // Arrange
        _clock.Now = OutsideIndexerWindow();
        var podcast = _fixture.CreatePodcast(p => p.IndexAllEpisodes = true);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.Film, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.FilmAutoIndexed);
        _destination.Films.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate rollback: without --apply it only reports, and with --apply but no consent it exits 2, " +
        "because rollback is also a production write.")]
    public async Task rollback_needs_apply_and_consent()
    {
        // Arrange
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();
        var applied = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));
        var file = applied.Evidence.Single();

        // Act
        var dryRun = await sut.Run(new CatalogueMigrateRequest { Rollback = file });
        var refused = await sut.Run(new CatalogueMigrateRequest { Rollback = file, Apply = true });

        // Assert
        dryRun.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        refused.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitApplyRefused);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        _destination.NewsOrganisations.Should().ContainKey(podcast.Id);
    }

    [Fact(DisplayName =
        "Catalogue migrate rollback: with --apply and the rollback consent phrase, the podcast and every episode are restored, " +
        "search goes back to Episode, and the destination rows are deleted, because the evidence snapshot is the restore source.")]
    public async Task rollback_restores_source_and_search()
    {
        // Arrange
        var (podcast, episodes) = SeedAllowlistedNewsPodcast(activeEpisodes: 2, removedEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();
        var applied = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));
        var file = applied.Evidence.Single();
        _clock.Now = _clock.Now.AddMinutes(5);

        // Act
        var result = await sut.Run(RollbackRequest(file));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        result.AppliedCount.Should().Be(1);
        (await _podcasts.GetPodcast(podcast.Id))!.Name.Should().Be(podcast.Name);
        (await _episodes.GetByPodcastId(podcast.Id).ToListAsync()).Select(e => e.Id)
            .Should().BeEquivalentTo(episodes.Select(e => e.Id));
        _destination.NewsOrganisations.Should().BeEmpty();
        _destination.NewsReports.Should().BeEmpty();
        _destination.Search.Values.Should().HaveCount(2)
            .And.OnlyContain(document => document.ContentKind == SearchContentKind.Episode);
    }

    [Fact(DisplayName =
        "Catalogue migrate rollback: when the destination parent now holds playables the batch did not write, then rollback " +
        "refuses that podcast, because deleting the parent would orphan newer rows.")]
    public async Task rollback_refuses_when_destination_grew()
    {
        // Arrange
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();
        var applied = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));
        var organisation = _destination.NewsOrganisations[podcast.Id];
        var later = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        await _destination.SaveNews(organisation, [CatalogueParentKindMapper.ToNewsReport(later, organisation)]);

        // Act
        var result = await sut.Run(RollbackRequest(applied.Evidence.Single()));

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitFailed);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        _destination.NewsReports.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "Catalogue migrate: a News apply keeps the podcast's YouTube channel id (and other service ids) on the stored " +
        "organisation's indexing, because a moved outlet that loses its channel id stops matching submits.")]
    public async Task news_apply_keeps_parent_service_ids()
    {
        // Arrange
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.AppliedCount.Should().Be(1);
        var indexing = _destination.NewsOrganisations[podcast.Id].Indexing;
        indexing.Should().NotBeNull();
        indexing!.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
        CatalogueMigrateServiceIds.Dropped(podcast, _destination.NewsOrganisations[podcast.Id]).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when another news organisation already has the destination name, then News apply is blocked " +
        "and nothing is written, because apply must attach to that outlet or stop, never mint a duplicate.")]
    public async Task existing_news_organisation_with_same_name_blocks_apply()
    {
        // Arrange
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var existing = CatalogueParentKindMapper.ToNewsOrganisation(_fixture.CreatePodcast(p => p.Name = podcast.Name));
        _destination.NewsOrganisations[existing.Id] = existing;
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.NewsReport));

        // Assert
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.ParentNameExists);
        _destination.NewsOrganisations.Keys.Should().Equal(existing.Id);
        _destination.NewsReports.Should().BeEmpty();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a publisher maps to a canonical TV show name that another TV show already has, then TV " +
        "apply is blocked, because the canonical name must attach to the existing show, not create a second one.")]
    public async Task canonical_tv_name_owned_by_another_show_blocks_apply()
    {
        // Arrange
        var (publisherName, showName) = CatalogueTvShowCanonicalNames.ShowNameByPublisherName
            .First(pair => !string.Equals(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase));
        var podcast = _fixture.CreatePodcast(p => p.Name = publisherName);
        _podcasts.Seed(podcast);
        _episodes.Seed(_fixture.CreateStoredEpisodeWithYouTubeOnly(podcast));
        var existing = CatalogueParentKindMapper.ToTvShow(_fixture.CreatePodcast(p => p.Name = showName));
        _destination.TvShows[existing.Id] = existing;
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.TvShowEpisode, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.ParentNameExists);
        _destination.TvShows.Keys.Should().Equal(existing.Id);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate rollback: a preview (no --apply) reports the plan without building the applier, because a " +
        "preview must never build the Search client or write.")]
    public async Task rollback_preview_does_not_build_the_applier()
    {
        // Arrange
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        var applied = await _mocker.CreateInstance<CatalogueMigrateProcessor>().Run(ApplyRequest(SubmitClassification.NewsReport));
        var applierBuilds = 0;
        _mocker.Use<Func<CatalogueMigrateApplier>>(() =>
        {
            applierBuilds++;
            return _mocker.CreateInstance<CatalogueMigrateApplier>();
        });
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Rollback = applied.Evidence.Single() });

        // Assert
        result.ExitCode.Should().Be(CatalogueMigrateProcessor.ExitOk);
        result.PlannedCount.Should().Be(1);
        applierBuilds.Should().Be(0);
        (await _podcasts.GetPodcast(podcast.Id)).Should().BeNull();
        _destination.NewsOrganisations.Should().ContainKey(podcast.Id);
    }

    private static DateTimeOffset AtMinute(int minute) =>
        new DateTimeOffset(DomainTestFixture.UtcToday, TimeSpan.Zero).AddHours(12).AddMinutes(minute);

    private static DateTimeOffset OutsideIndexerWindow() => AtMinute(CatalogueMigrateIndexerWindow.SafeFromMinute);

    private CatalogueMigrateRequest ApplyRequest(
        string kind,
        string? consentKind = null,
        Guid? podcastId = null,
        int batchSize = CatalogueMigrateRequest.DefaultBatchSize,
        int? limit = null) =>
        new()
        {
            Kind = kind,
            PodcastId = podcastId,
            Apply = true,
            Consent = CatalogueMigrateConsent.ApplyPhrase(consentKind ?? kind),
            BatchSize = batchSize,
            Limit = limit
        };

    private static CatalogueMigrateRequest RollbackRequest(string file) =>
        new()
        {
            Rollback = file,
            Apply = true,
            Consent = CatalogueMigrateConsent.RollbackPhrase(Path.GetFileName(file))
        };

    private (Podcast Podcast, IReadOnlyList<Episode> Episodes) SeedAllowlistedNewsPodcast(
        int activeEpisodes,
        int removedEpisodes = 0)
    {
        var name = CatalogueNewsOrganisationAllowlist.Names.First(candidate =>
            IsYouTubeOnlyNewsCandidate(candidate)
            && !_podcasts.GetAll().ToBlockingEnumerable().Any(existing => existing.Name == candidate));
        return SeedYouTubeOnlyPodcast(name, activeEpisodes, removedEpisodes);
    }

    private Podcast SeedAutoIndexedAllowlistedNewsPodcast(Action<Podcast>? configure = null)
    {
        var (podcast, _) = SeedAllowlistedNewsPodcast(activeEpisodes: 1);
        podcast.IndexAllEpisodes = true;
        configure?.Invoke(podcast);
        _podcasts.Seed(podcast);
        return podcast;
    }

    private Podcast SeedHeuristicNewsPodcastOffAllowlist()
    {
        var name = $"{_fixture.CreateShortTitle()} News";
        IsYouTubeOnlyNewsCandidate(name).Should().BeTrue();
        CatalogueNewsOrganisationAllowlist.Contains(name).Should().BeFalse();
        return SeedYouTubeOnlyPodcast(name, activeEpisodes: 1, removedEpisodes: 0).Podcast;
    }

    private bool IsYouTubeOnlyNewsCandidate(string name)
    {
        var probe = CreateYouTubeOnlyPodcast(name);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(probe);
        var suggestion = CatalogueMigrateIdentify.FromPodcast(probe, [episode]);
        return suggestion.ContentKind == SubmitClassification.NewsReport && !suggestion.RequiresCurator;
    }

    private (Podcast Podcast, IReadOnlyList<Episode> Episodes) SeedYouTubeOnlyPodcast(
        string name,
        int activeEpisodes,
        int removedEpisodes)
    {
        var podcast = CreateYouTubeOnlyPodcast(name);
        var episodes = Enumerable.Range(0, activeEpisodes)
            .Select(_ => _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast))
            .Concat(Enumerable.Range(0, removedEpisodes)
                .Select(_ => CreateRemovedYouTubeOnlyEpisode(podcast)))
            .ToList();
        _podcasts.Seed(podcast);
        _episodes.Seed([.. episodes]);
        return (podcast, episodes);
    }

    private Episode CreateRemovedYouTubeOnlyEpisode(Podcast podcast)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Removed = true;
        return episode;
    }

    private Podcast CreateYouTubeOnlyPodcast(string name) =>
        _fixture.CreatePodcast(p =>
        {
            p.Name = name;
            p.IndexAllEpisodes = false;
            p.EpisodeIncludeTitleRegex = string.Empty;
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
}
