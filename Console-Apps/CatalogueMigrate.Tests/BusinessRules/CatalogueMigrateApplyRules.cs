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
        _mocker.Use<ICatalogueMigrateEvidenceStore>(_evidence);
        _mocker.Use<TimeProvider>(_clock);
        _mocker.Use(NullLogger<CatalogueMigrateProcessor>.Instance);
        _mocker.Use(NullLogger<CatalogueMigrateApplier>.Instance);
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
        "Catalogue migrate: when the podcast is auto-indexed, then apply is blocked and the plan line says why, " +
        "because Film, TV, and News parents are not indexed and new episodes would stop arriving.")]
    public async Task auto_indexed_podcast_is_blocked()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.IndexAllEpisodes = true);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(ApplyRequest(SubmitClassification.TvShowEpisode, podcastId: podcast.Id));

        // Assert
        result.AppliedCount.Should().Be(0);
        result.PlanLines.Single().BlockReason.Should().Be(CatalogueMigratePlanner.AutoIndexed);
        _destination.TvShows.Should().BeEmpty();
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
