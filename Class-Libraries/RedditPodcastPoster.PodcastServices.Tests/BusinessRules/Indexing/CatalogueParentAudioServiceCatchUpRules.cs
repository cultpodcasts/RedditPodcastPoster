using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Spotify / Apple catch-up for news and TV parents. Spotify discovery only runs on even hours
/// (IndexingStrategy.IndexSpotify) and parent batches 3-4 run on odd hours, and Apple discovery is skipped when Spotify
/// handled the pass, so the parent updater must offer reports missing a link to the podcast enrichers on every pass.
/// </summary>
public class CatalogueParentAudioServiceCatchUpRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(60);
    private static readonly TimeSpan FullLength = TimeSpan.FromMinutes(42);
    private static readonly TimeSpan ClipLength = TimeSpan.FromMinutes(1);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private readonly Dictionary<Guid, TvShow> _shows = [];
    private readonly List<NewsReport> _savedReports = [];
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private List<NewsReport> _storedReports = [];
    private List<TvShowEpisode> _storedTvEpisodes = [];
    private readonly List<(Guid EpisodeId, IReadOnlyList<Episode> Window)> _spotifyCalls = [];
    private readonly List<Guid> _appleCalls = [];
    private Action<EnrichmentRequest, EnrichmentContext> _spotifyMatch = (_, _) => { };
    private Action<EnrichmentRequest, EnrichmentContext> _appleMatch = (_, _) => { };
    private Action<IndexingContext> _onSpotifyLookup = _ => { };
    private List<NewsReport> _holderReports = [];
    private int _reportReads;
    private List<Episode> _discovered = [];
    private Action<IReadOnlyList<Episode>> _onMerge = _ => { };
    private readonly List<(Episode Existing, Episode NewDetails)> _merged = [];
    private readonly SteppingTimeProvider _clock = new(DateTime.UtcNow);

    public CatalogueParentAudioServiceCatchUpRules()
    {
        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use<TimeProvider>(_clock);

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        organisations.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => _organisations.Values.ToAsyncEnumerable());
        organisations.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .ReturnsAsync(true);
        var tvShows = _mocker.GetMock<ITvShowRepository>();
        tvShows.Setup(x => x.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.GetValueOrDefault(id));
        tvShows.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(() => _shows.Values.ToAsyncEnumerable());
        tvShows.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .ReturnsAsync(true);

        var reports = _mocker.GetMock<INewsReportRepository>();
        // First read per pass is the release window, any second read is the Spotify / Apple link holders read.
        reports.Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Guid id, Expression<Func<NewsReport, bool>> _) =>
                (_reportReads++ == 0 ? _storedReports : _holderReports)
                .Where(x => x.NewsOrganisationId == id).ToAsyncEnumerable());
        reports.Setup(x => x.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback((IEnumerable<NewsReport> items) => _savedReports.AddRange(items))
            .Returns(Task.CompletedTask);
        var tvEpisodes = _mocker.GetMock<ITvShowEpisodeRepository>();
        tvEpisodes.Setup(x => x.GetByTvShowId(It.IsAny<Guid>(), It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Guid id, Expression<Func<TvShowEpisode, bool>> _) =>
                _storedTvEpisodes.Where(x => x.TvShowId == id).ToAsyncEnumerable());
        tvEpisodes.Setup(x => x.Save(It.IsAny<IEnumerable<TvShowEpisode>>()))
            .Callback((IEnumerable<TvShowEpisode> items) => _savedTvEpisodes.AddRange(items))
            .Returns(Task.CompletedTask);

        // By default no discovery and no merge: the catch-up step alone must fill the gap.
        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(() => [.. _discovered]);
        _mocker.GetMock<IEpisodeMerger>()
            .Setup(x => x.MergeEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IEnumerable<Episode>>()))
            .Returns((Podcast _, IEnumerable<Episode> existing, IEnumerable<Episode> _) =>
            {
                var existingList = existing.ToList();
                _onMerge(existingList);
                return new EpisodeMergeResult(existingList, [.. _discovered], [.. _merged], []);
            });
        _mocker.GetMock<IPodcastFilter>()
            .Setup(x => x.Filter(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<List<string>>()))
            .Returns(new FilterResult([]));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<Podcast>());
        _mocker.GetMock<IEliminationTermsProvider>()
            .Setup(x => x.GetEliminationTerms())
            .Returns(new EliminationTerms());
        _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>()
            .Setup(x => x.GetAsync())
            .ReturnsAsync(() => _mocker.GetMock<IEliminationTermsProvider>().Object);

        _mocker.GetMock<ISpotifyEpisodeEnricher>()
            .Setup(x => x.Enrich(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>(), It.IsAny<EnrichmentContext>()))
            .Callback((EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext context) =>
            {
                _spotifyCalls.Add((request.Episode.Id, request.Episodes.ToList()));
                _onSpotifyLookup(indexingContext);
                _spotifyMatch(request, context);
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IAppleEpisodeEnricher>()
            .Setup(x => x.Enrich(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>(), It.IsAny<EnrichmentContext>()))
            .Callback((EnrichmentRequest request, IndexingContext _, EnrichmentContext context) =>
            {
                _appleCalls.Add(request.Episode.Id);
                _appleMatch(request, context);
            })
            .Returns(Task.CompletedTask);
    }

    private CatalogueParentsUpdater Sut => _mocker.CreateInstance<CatalogueParentsUpdater>();

    /// <summary>Odd-hour pass (parent batches 3-4): Spotify discovery is off.</summary>
    private static IndexingContext OddHourPass() => new(ReleasedSince, IndexSpotify: false);

    /// <summary>Even-hour pass (parent batches 1-2): Spotify discovery is on, so Apple discovery is skipped.</summary>
    private static IndexingContext EvenHourPass() => new(ReleasedSince, IndexSpotify: true);

    private Podcast ParentPodcast(bool spotifyShow = true, bool applePodcast = true)
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.IndexAllEpisodes = true;
        podcast.SpotifyId = spotifyShow ? _fixture.CreateSpotifyId() : string.Empty;
        podcast.AppleId = applePodcast ? _fixture.CreateAppleId() : null;
        return podcast;
    }

    private (NewsOrganisation Organisation, Podcast Podcast) SeedOrganisation(Podcast podcast)
    {
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        _organisations[organisation.Id] = organisation;
        return (organisation, podcast);
    }

    private NewsReport StoredYouTubeOnlyReport(Podcast podcast, NewsOrganisation organisation, TimeSpan length,
        DateTime? release = null)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, release ?? DomainTestFixture.UtcDateDaysAgo(2), length);
        return CatalogueParentKindMapper.ToNewsReport(episode, organisation);
    }

    /// <summary>Apple episode url as the catalogue stores it (episode id in <c>?i=</c>).</summary>
    private static Uri AppleEpisodeUrl(long appleEpisodeId) =>
        new($"https://podcasts.apple.com/gb/podcast/synthetic-show/id1000000001?i={appleEpisodeId}");

    private void SpotifyFinds(string spotifyId) =>
        _spotifyMatch = (request, context) =>
        {
            // Same rule as SpotifyEpisodeEnricher: an id already held by an item in the window is never reassigned.
            if (request.Episodes.Any(x => EpisodeServicePresence.SpotifyEpisodeId(x) == spotifyId))
            {
                return;
            }

            var url = _fixture.DefaultSpotifyUrl(spotifyId);
            EpisodeServicePresence.SetSpotifyIdentity(request.Episode, spotifyId);
            EpisodeServicePresence.Upsert(request.Episode, ServiceKeys.Spotify, url, null);
            context.Spotify = url;
        };

    private void AppleFinds(long appleEpisodeId) =>
        _appleMatch = (request, context) =>
        {
            var url = AppleEpisodeUrl(appleEpisodeId);
            EpisodeServicePresence.SetAppleIdentity(request.Episode, appleEpisodeId);
            EpisodeServicePresence.Upsert(request.Episode, ServiceKeys.Apple, url, null);
            context.Apple = url;
        };

    [Fact(DisplayName =
        "Catalogue parent audio catch-up: when an odd-hour pass (batches 3-4, Spotify discovery off) indexes a news " +
        "organisation with a Spotify show and a stored report has no Spotify link, then Spotify is looked up and the " +
        "report is saved with the Spotify url, because otherwise passes 3-4 never get Spotify.")]
    public async Task odd_hour_pass_looks_up_spotify_for_report_missing_it()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast());
        var report = StoredYouTubeOnlyReport(podcast, organisation, FullLength);
        _storedReports = [report];
        var spotifyId = _fixture.CreateSpotifyId();
        SpotifyFinds(spotifyId);

        // Act
        var success = await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        success.Should().BeTrue();
        _spotifyCalls.Should().ContainSingle().Which.EpisodeId.Should().Be(report.Id);
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Id.Should().Be(report.Id);
        saved.Services.Should().ContainKey(ServiceKeys.Spotify);
        saved.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
        saved.Services.Should().ContainKey(ServiceKeys.YouTube);
        saved.YouTubeId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up: when an even-hour pass (batches 1-2, Spotify discovery handles the pass so " +
        "Apple discovery is skipped) indexes a report that has Spotify but no Apple link, then Apple is looked up, " +
        "Spotify is not, and the report is saved with the Apple url, because otherwise batches 1-2 never get Apple.")]
    public async Task even_hour_pass_looks_up_apple_for_report_missing_it()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast());
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(2), FullLength);
        var ownSpotifyId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(episode, ownSpotifyId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(ownSpotifyId), null);
        var report = CatalogueParentKindMapper.ToNewsReport(episode, organisation);
        _storedReports = [report];
        var appleEpisodeId = _fixture.CreateAppleId();
        AppleFinds(appleEpisodeId);

        // Act
        await Sut.UpdateParents([organisation.Id], EvenHourPass());

        // Assert
        _spotifyCalls.Should().BeEmpty();
        _appleCalls.Should().ContainSingle().Which.Should().Be(report.Id);
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Services![ServiceKeys.Apple].Url.Should().Be(AppleEpisodeUrl(appleEpisodeId));
        saved.Services[ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(ownSpotifyId));
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up: when a TV show episode lacks Spotify on an odd-hour pass, then Spotify is " +
        "looked up and the TvShowEpisode is saved with it, because TV parents share the same batches.")]
    public async Task odd_hour_pass_looks_up_spotify_for_tv_show_episode()
    {
        // Arrange
        var podcast = ParentPodcast(applePodcast: false);
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        _shows[show.Id] = show;
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(2), FullLength);
        var stored = CatalogueParentKindMapper.ToTvShowEpisode(episode, show);
        _storedTvEpisodes = [stored];
        var spotifyId = _fixture.CreateSpotifyId();
        SpotifyFinds(spotifyId);

        // Act
        await Sut.UpdateParents([show.Id], OddHourPass());

        // Assert
        _appleCalls.Should().BeEmpty();
        _savedTvEpisodes.Should().ContainSingle().Which.Services![ServiceKeys.Spotify].Url
            .Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a full report and a one-minute clip of it are both missing " +
        "Spotify, then only the full report is looked up, because a clip is not the podcast episode and must never " +
        "be offered a chance to take its full episode's link.")]
    public async Task short_clip_is_not_looked_up()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        var release = DomainTestFixture.UtcDateDaysAgo(2);
        var clip = StoredYouTubeOnlyReport(podcast, organisation, ClipLength, release.AddMinutes(30));
        var full = StoredYouTubeOnlyReport(podcast, organisation, FullLength, release);
        _storedReports = [clip, full];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Select(x => x.EpisodeId).Should().Equal(full.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a Spotify match also carries a different title, description " +
        "and length, then only the service links are written to the stored report, because a catch-up must never " +
        "overwrite what curators or discovery stored for the report.")]
    public async Task catch_up_writes_only_service_links()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        var report = StoredYouTubeOnlyReport(podcast, organisation, FullLength);
        var storedTitle = report.Title;
        var storedDescription = report.Description;
        var storedRelease = report.Release;
        _storedReports = [report];
        var spotifyId = _fixture.CreateSpotifyId();
        SpotifyFinds(spotifyId);
        var match = _spotifyMatch;
        _spotifyMatch = (request, context) =>
        {
            match(request, context);
            request.Episode.Title = "Different Spotify title";
            request.Episode.Description = "Different Spotify description";
            request.Episode.Length = FullLength + TimeSpan.FromMinutes(9);
            request.Episode.ReleaseUtc = request.Episode.ReleaseUtc.AddDays(1);
        };

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Title.Should().Be(storedTitle);
        saved.Description.Should().Be(storedDescription);
        saved.Length.Should().Be(FullLength);
        saved.Release.Should().Be(storedRelease);
        saved.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when the Spotify lookup finds no match, then nothing is saved, " +
        "because a miss must not cost a playable write or touch the report.")]
    public async Task spotify_miss_saves_nothing()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        _storedReports = [StoredYouTubeOnlyReport(podcast, organisation, FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Should().ContainSingle();
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a report already has Spotify and Apple links, then neither is " +
        "looked up again, because an existing link must never be replaced by a catch-up.")]
    public async Task report_with_both_links_is_not_looked_up()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast());
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(2), FullLength);
        var spotifyId = _fixture.CreateSpotifyId();
        var appleEpisodeId = _fixture.CreateAppleId();
        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(spotifyId), null);
        EpisodeServicePresence.SetAppleIdentity(episode, appleEpisodeId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, AppleEpisodeUrl(appleEpisodeId), null);
        _storedReports = [CatalogueParentKindMapper.ToNewsReport(episode, organisation)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Should().BeEmpty();
        _appleCalls.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when the parent has no Spotify show id and no Apple podcast id, then " +
        "no lookup is made, because a YouTube-only news organisation must not cost Spotify or Apple API calls.")]
    public async Task parent_without_audio_ids_is_not_looked_up()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(spotifyShow: false, applePodcast: false));
        _storedReports = [StoredYouTubeOnlyReport(podcast, organisation, FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Should().BeEmpty();
        _appleCalls.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a report is removed, then it is not looked up, because a " +
        "removed report must not be revived or linked by a catch-up.")]
    public async Task removed_report_is_not_looked_up()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast());
        var report = StoredYouTubeOnlyReport(podcast, organisation, FullLength);
        report.Removed = true;
        _storedReports = [report];
        SpotifyFinds(_fixture.CreateSpotifyId());

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Should().BeEmpty();
        _appleCalls.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a stored report outside the release window already holds a " +
        "Spotify link, then it is passed to the enricher with the window and the candidate is not given that link, " +
        "because a link already owned by any stored report must never be assigned to a second report.")]
    public async Task link_holder_outside_window_is_offered_with_the_window()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        var candidate = StoredYouTubeOnlyReport(podcast, organisation, FullLength, DomainTestFixture.UtcDateDaysAgo(1));
        var heldSpotifyId = _fixture.CreateSpotifyId();
        var holderEpisode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, DomainTestFixture.UtcDateDaysAgo(10), FullLength);
        EpisodeServicePresence.SetSpotifyIdentity(holderEpisode, heldSpotifyId);
        EpisodeServicePresence.Upsert(holderEpisode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(heldSpotifyId), null);
        var holder = CatalogueParentKindMapper.ToNewsReport(holderEpisode, organisation);
        _storedReports = [candidate];
        _holderReports = [holder];
        SpotifyFinds(heldSpotifyId);

        // Act
        await Sut.UpdateParents(
            [organisation.Id], new IndexingContext(DomainTestFixture.UtcDateDaysAgo(3), IndexSpotify: false));

        // Assert
        _reportReads.Should().Be(2);
        _spotifyCalls.Should().ContainSingle().Which.Window.Select(x => x.Id)
            .Should().BeEquivalentTo([candidate.Id, holder.Id]);
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when no report in the window needs a lookup, then the link holders " +
        "are not read, because a parent with nothing to catch up must not cost an extra Cosmos query each pass.")]
    public async Task holders_are_not_read_without_candidates()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(2), FullLength);
        var spotifyId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(spotifyId), null);
        _storedReports = [CatalogueParentKindMapper.ToNewsReport(episode, organisation)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _reportReads.Should().Be(1);
        _spotifyCalls.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when more recent reports lack Spotify than the per-pass cap, then only " +
        "the newest ones up to the cap are looked up, because the catch-up must not spend the parent's budget on a " +
        "backlog (the rest are offered on later passes).")]
    public async Task lookups_are_capped_per_pass_newest_first()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        var newest = DomainTestFixture.UtcDateDaysAgo(1);
        var reports = Enumerable.Range(0, CatalogueParentsUpdater.AudioCatchUpMaxCandidatesPerPass + 2)
            .Select(i => StoredYouTubeOnlyReport(podcast, organisation, FullLength, newest.AddHours(-6 * i)))
            .ToList();
        _storedReports = reports;

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Select(x => x.EpisodeId).Should().BeEquivalentTo(
            reports.Take(CatalogueParentsUpdater.AudioCatchUpMaxCandidatesPerPass).Select(x => x.Id));
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a report missing Spotify was released before the catch-up's " +
        "recency window, then it is not looked up, because it already had its chances on earlier passes and must not " +
        "cost a lookup every hour.")]
    public async Task report_older_than_recency_window_is_not_looked_up()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        var old = StoredYouTubeOnlyReport(podcast, organisation, FullLength,
            DomainTestFixture.UtcDateDaysAgo(CatalogueParentsUpdater.AudioCatchUpRecency.Days + 3));
        var recent = StoredYouTubeOnlyReport(podcast, organisation, FullLength);
        _storedReports = [old, recent];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        _spotifyCalls.Select(x => x.EpisodeId).Should().Equal(recent.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when the catch-up passes its share of the parent's budget, then the " +
        "remaining lookups are skipped and the newly discovered report is still saved, because an optional catch-up " +
        "must never cost a pass its discovered items.")]
    public async Task budget_soft_stop_skips_remaining_lookups_and_keeps_discovered_report()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        _storedReports =
        [
            StoredYouTubeOnlyReport(podcast, organisation, FullLength, DomainTestFixture.UtcDateDaysAgo(2)),
            StoredYouTubeOnlyReport(podcast, organisation, FullLength, DomainTestFixture.UtcDateDaysAgo(3))
        ];
        var discovered = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(1), FullLength);
        _discovered = [discovered];
        var budget = new PodcastUpdateBudgetOptions().BudgetFor(false)!.Value;
        _onSpotifyLookup = _ => _clock.Advance(budget * CatalogueParentsUpdater.AudioCatchUpBudgetShare);

        // Act
        var success = await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        success.Should().BeTrue();
        _spotifyCalls.Should().ContainSingle();
        _savedReports.Should().ContainSingle().Which.Id.Should().Be(discovered.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a Spotify rate limit is hit during the catch-up, then Spotify is " +
        "not called again this pass and lastIndexed still moves, because discovery completed and the optional " +
        "catch-up must not mark the pass as bypassed.")]
    public async Task spotify_rate_limit_in_catch_up_does_not_mark_pass_bypassed()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        _storedReports =
        [
            StoredYouTubeOnlyReport(podcast, organisation, FullLength, DomainTestFixture.UtcDateDaysAgo(1)),
            StoredYouTubeOnlyReport(podcast, organisation, FullLength, DomainTestFixture.UtcDateDaysAgo(2))
        ];
        _onSpotifyLookup = context => context.SkipSpotifyUrlResolving = true;

        // Act
        var success = await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        success.Should().BeTrue();
        _spotifyCalls.Should().ContainSingle();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(
            x => x.PatchIndexerState(organisation.Id, It.Is<CatalogueParentIndexerPatch>(p => p.LastIndexed != null)),
            Times.Once);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when a lookup throws, then the pass still succeeds and saves its " +
        "discovered report, because a failing optional catch-up must not fail the parent's indexing.")]
    public async Task failing_lookup_does_not_fail_the_pass()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(applePodcast: false));
        _storedReports = [StoredYouTubeOnlyReport(podcast, organisation, FullLength)];
        var discovered = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(1), FullLength);
        _discovered = [discovered];
        _onSpotifyLookup = _ => throw new InvalidOperationException("synthetic lookup failure");

        // Act
        var success = await Sut.UpdateParents([organisation.Id], OddHourPass());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Id.Should().Be(discovered.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard: when discovery merges new details into a report and the Apple match " +
        "carries a different release and description, then the report is saved with the merged details and the " +
        "Apple link only, because the catch-up must never overwrite what the merge just wrote.")]
    public async Task merged_details_survive_apple_catch_up()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(ParentPodcast(spotifyShow: false));
        var report = StoredYouTubeOnlyReport(podcast, organisation, FullLength);
        var storedRelease = report.Release;
        _storedReports = [report];
        const string mergedTitle = "Synthetic merged title";
        const string mergedDescription = "Synthetic merged description";
        _onMerge = existing =>
        {
            var target = existing.Single(x => x.Id == report.Id);
            target.Title = mergedTitle;
            target.Description = mergedDescription;
            _merged.Add((target, target));
        };
        var appleEpisodeId = _fixture.CreateAppleId();
        AppleFinds(appleEpisodeId);
        var match = _appleMatch;
        _appleMatch = (request, context) =>
        {
            match(request, context);
            request.Episode.ReleaseUtc = request.Episode.ReleaseUtc.AddHours(-20);
            request.Episode.Description = "Different Apple description";
        };

        // Act
        await Sut.UpdateParents([organisation.Id], EvenHourPass());

        // Assert
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Title.Should().Be(mergedTitle);
        saved.Description.Should().Be(mergedDescription);
        saved.Release.Should().Be(storedRelease);
        saved.Services![ServiceKeys.Apple].Url.Should().Be(AppleEpisodeUrl(appleEpisodeId));
    }

    /// <summary>Clock whose wall time and timestamps only move when a rule advances them.</summary>
    private sealed class SteppingTimeProvider(DateTime utcStart) : TimeProvider
    {
        private readonly long _startTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
        private TimeSpan _offset;

        public void Advance(TimeSpan by) => _offset += by;

        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(DateTime.SpecifyKind(utcStart, DateTimeKind.Utc)) + _offset;

        public override long GetTimestamp() =>
            _startTimestamp + (long)(_offset.TotalSeconds * TimestampFrequency);
    }
}
