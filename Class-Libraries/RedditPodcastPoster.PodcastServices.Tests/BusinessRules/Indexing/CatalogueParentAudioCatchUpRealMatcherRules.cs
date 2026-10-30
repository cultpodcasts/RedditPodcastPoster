using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Catalogue parent Spotify / Apple catch-up through the REAL <see cref="SpotifyEpisodeEnricher"/> /
/// <see cref="AppleEpisodeEnricher"/>, the real resolvers, lookup executor and platform matcher; only the Spotify and
/// Apple HTTP clients and Cosmos are faked. These are the guards that a clip, a report whose link another stored
/// report holds, or a report from another day is never given a link, so they must not use a stubbed enricher.
/// </summary>
public class CatalogueParentAudioCatchUpRealMatcherRules
{
    private static readonly TimeSpan FullLength = TimeSpan.FromMinutes(42);
    private static readonly TimeSpan ClipLength = TimeSpan.FromMinutes(6);
    private const string StoryTitle = "Synthetic Harbour Inquiry Part Three The Missing Ledger";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private readonly List<NewsReport> _savedReports = [];
    private List<NewsReport> _storedReports = [];
    private List<NewsReport> _holderReports = [];
    private int _reportReads;
    private List<SimpleEpisode> _spotifyShow = [];
    private List<AppleEpisode> _appleShow = [];

    public CatalogueParentAudioCatchUpRealMatcherRules()
    {
        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use<TimeProvider>(TimeProvider.System);

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        organisations.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => _organisations.Values.ToAsyncEnumerable());
        organisations.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .ReturnsAsync(true);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<RedditPodcastPoster.Models.TvShows.TvShow, bool>>>()))
            .Returns(() => AsyncEnumerable.Empty<RedditPodcastPoster.Models.TvShows.TvShow>());

        // First read per pass is the release window, any second read is the Spotify / Apple link holders read.
        var reports = _mocker.GetMock<INewsReportRepository>();
        reports.Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Guid id, Expression<Func<NewsReport, bool>> _) =>
                (_reportReads++ == 0 ? _storedReports : _holderReports)
                .Where(x => x.NewsOrganisationId == id).ToAsyncEnumerable());
        reports.Setup(x => x.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback((IEnumerable<NewsReport> items) => _savedReports.AddRange(items))
            .Returns(Task.CompletedTask);

        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IEpisodeMerger>()
            .Setup(x => x.MergeEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IEnumerable<Episode>>()))
            .Returns((Podcast _, IEnumerable<Episode> existing, IEnumerable<Episode> _) =>
                new EpisodeMergeResult(existing.ToList(), [], [], []));
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

        _mocker.Use<ISpotifyEpisodeEnricher>(CreateSpotifyEnricher());
        _mocker.Use<IAppleEpisodeEnricher>(CreateAppleEnricher());
    }

    private CatalogueParentsUpdater Sut => _mocker.CreateInstance<CatalogueParentsUpdater>();

    private static IndexingContext OddHourPass(DateTime releasedSince) => new(releasedSince, IndexSpotify: false);

    private SpotifyEpisodeEnricher CreateSpotifyEnricher()
    {
        var client = _mocker.GetMock<ISpotifyClientWrapper>();
        client.Setup(x => x.GetShowEpisodes(It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, ShowEpisodesRequest r, IndexingContext _, CancellationToken _) =>
                new Paging<SimpleEpisode>
                {
                    Items = _spotifyShow.Skip(r.Offset ?? 0).Take(r.Limit ?? 20).ToList(),
                    Total = _spotifyShow.Count
                });
        client.Setup(x => x.GetFullEpisode(It.IsAny<string>(), It.IsAny<EpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, EpisodeRequest _, IndexingContext _, CancellationToken _) =>
                _spotifyShow.Where(x => x.Id == id).Select(ToFullEpisode).FirstOrDefault());
        _mocker.GetMock<ISpotifyEpisodeTitleSearch>()
            .Setup(x => x.FindCandidates(It.IsAny<FindSpotifyEpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var guard = CatalogueTestServices.CreateGuard();
        var sanitiser = new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance);
        var resolver = new SpotifyEpisodeResolver(
            _mocker.GetMock<ISpotifyEpisodeTitleSearch>().Object,
            client.Object,
            new SpotifyShowEpisodePages(client.Object, guard),
            CatalogueTestServices.CreateExecutor(),
            guard,
            sanitiser,
            NullLogger<SpotifyEpisodeResolver>.Instance);
        return new SpotifyEpisodeEnricher(
            resolver,
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new SpotifyEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            new SpotifyExpensiveQuerySideEffect(NullLogger<SpotifyExpensiveQuerySideEffect>.Instance),
            sanitiser,
            NullLogger<SpotifyEpisodeEnricher>.Instance);
    }

    private AppleEpisodeEnricher CreateAppleEnricher()
    {
        var service = _mocker.GetMock<IApplePodcastService>();
        service.Setup(x => x.GetEpisodePage(It.IsAny<ApplePodcastId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplePodcastId _, string? next, CancellationToken _) =>
                next == null ? new AppleEpisodePage(_appleShow.OrderByDescending(x => x.Release).ToList(), null) : null);
        var cached = _mocker.GetMock<ICachedApplePodcastService>();
        cached.Setup(x => x.GetEpisodes(It.IsAny<ApplePodcastId>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _appleShow);
        cached.Setup(x => x.GetEpisode(It.IsAny<ApplePodcastId>(), It.IsAny<long>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplePodcastId _, long id, IndexingContext _, CancellationToken _) =>
                _appleShow.FirstOrDefault(x => x.Id == id));
        var lookup = _mocker.GetMock<IAppleCatalogueLookup>();
        lookup.Setup(x => x.GetEpisodeCount(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _appleShow.Count);
        lookup.Setup(x => x.SearchShowEpisodes(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var resolver = new AppleEpisodeResolver(
            cached.Object,
            new AppleShowEpisodePages(service.Object),
            lookup.Object,
            CatalogueTestServices.CreateExecutor(),
            NullLogger<AppleEpisodeResolver>.Instance);
        return new AppleEpisodeEnricher(
            _mocker.GetMock<IApplePodcastEnricher>().Object,
            resolver,
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new AppleEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            NullLogger<AppleEpisodeEnricher>.Instance);
    }

    private (NewsOrganisation Organisation, Podcast Podcast) SeedOrganisation(bool spotifyShow, bool applePodcast)
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.IndexAllEpisodes = true;
        podcast.SpotifyId = spotifyShow ? _fixture.CreateSpotifyId() : string.Empty;
        podcast.AppleId = applePodcast ? _fixture.CreateAppleId() : null;
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        _organisations[organisation.Id] = organisation;
        return (organisation, podcast);
    }

    private NewsReport Report(Podcast podcast, NewsOrganisation organisation, DateTime release, TimeSpan length,
        string title = StoryTitle) =>
        CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release, length, title), organisation);

    private NewsReport SpotifyHolder(Podcast podcast, NewsOrganisation organisation, DateTime release, string spotifyId)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release, FullLength, StoryTitle);
        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(spotifyId), null);
        return CatalogueParentKindMapper.ToNewsReport(episode, organisation);
    }

    private NewsReport AppleHolder(Podcast podcast, NewsOrganisation organisation, DateTime release, long appleEpisodeId)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release, FullLength, StoryTitle);
        EpisodeServicePresence.SetAppleIdentity(episode, appleEpisodeId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, AppleUrl(podcast, appleEpisodeId), null);
        return CatalogueParentKindMapper.ToNewsReport(episode, organisation);
    }

    private SimpleEpisode SpotifyEpisode(string id, DateTime release, TimeSpan length, string title = StoryTitle) => new()
    {
        Id = id,
        Name = title,
        HtmlDescription = "<p>Synthetic description</p>",
        DurationMs = (int)length.TotalMilliseconds,
        ReleaseDate = release.ToString("yyyy-MM-dd"),
        Type = ItemType.Episode,
        IsPlayable = true,
        ExternalUrls = new Dictionary<string, string> { ["spotify"] = _fixture.DefaultSpotifyUrl(id).ToString() },
        Images = []
    };

    private static FullEpisode ToFullEpisode(SimpleEpisode episode) => new()
    {
        Id = episode.Id,
        Name = episode.Name,
        HtmlDescription = episode.HtmlDescription,
        DurationMs = episode.DurationMs,
        ReleaseDate = episode.ReleaseDate,
        IsPlayable = true,
        ExternalUrls = episode.ExternalUrls,
        Images = []
    };

    private static Uri AppleUrl(Podcast podcast, long appleEpisodeId) =>
        new($"https://podcasts.apple.com/gb/podcast/synthetic-show/id{podcast.AppleId}?i={appleEpisodeId}");

    private static AppleEpisode AppleEpisode(Podcast podcast, long id, DateTime release, TimeSpan length) =>
        new(id, StoryTitle, release, length, AppleUrl(podcast, id), "Synthetic description", false);

    [Fact(DisplayName =
        "Catalogue parent audio catch-up, real Spotify matcher: when a report missing Spotify matches a same-day " +
        "Spotify episode of the same title and length, then the report is saved with that link, because the real " +
        "enricher and resolver must still fill a genuine gap (control for the guards below).")]
    public async Task real_spotify_match_fills_a_genuine_gap()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(spotifyShow: true, applePodcast: false);
        var release = DomainTestFixture.UtcDateDaysAgo(2).AddHours(9);
        var report = Report(podcast, organisation, release, FullLength);
        _storedReports = [report];
        var spotifyId = _fixture.CreateSpotifyId();
        _spotifyShow = [SpotifyEpisode(spotifyId, release, FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass(DomainTestFixture.UtcDateDaysAgo(30)));

        // Assert
        _savedReports.Should().ContainSingle().Which.Services![ServiceKeys.Spotify].Url
            .Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard, real Spotify matcher: when a 42-minute report and a 6-minute clip of " +
        "the same story are both missing Spotify and the show has only the 42-minute episode, then only the full " +
        "report gets the link, because a clip must never take its full episode's Spotify link.")]
    public async Task real_spotify_clip_does_not_take_full_episode_link()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(spotifyShow: true, applePodcast: false);
        var release = DomainTestFixture.UtcDateDaysAgo(2).AddHours(9);
        var full = Report(podcast, organisation, release, FullLength);
        var clip = Report(podcast, organisation, release.AddHours(1), ClipLength);
        _storedReports = [clip, full];
        var spotifyId = _fixture.CreateSpotifyId();
        _spotifyShow = [SpotifyEpisode(spotifyId, release, FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass(DomainTestFixture.UtcDateDaysAgo(30)));

        // Assert
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Id.Should().Be(full.Id);
        saved.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
        clip.Services.Should().NotContainKey(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard, real Spotify matcher: when a same-day report of the same title " +
        "released just before the window already holds the show's Spotify episode, then the report in the window is " +
        "not given that link, because one Spotify episode must never be linked to two reports.")]
    public async Task real_spotify_link_held_outside_window_is_not_reassigned()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(spotifyShow: true, applePodcast: false);
        var day = DomainTestFixture.UtcDateDaysAgo(2);
        var spotifyId = _fixture.CreateSpotifyId();
        var holder = SpotifyHolder(podcast, organisation, day.AddHours(6), spotifyId);
        var candidate = Report(podcast, organisation, day.AddHours(12), FullLength);
        _storedReports = [candidate];
        _holderReports = [holder];
        _spotifyShow = [SpotifyEpisode(spotifyId, day, FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass(day.AddHours(9)));

        // Assert
        _reportReads.Should().Be(2);
        _savedReports.Should().BeEmpty();
        candidate.Services.Should().NotContainKey(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard, real Spotify matcher: when the show's only episode with the same " +
        "title was released outside the matcher's release tolerance (14 days for a YouTube-authority parent without " +
        "a publishing delay), then the report is not given its link, because a report must only be linked to the " +
        "episode of its own release.")]
    public async Task real_spotify_same_title_on_another_day_is_not_linked()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(spotifyShow: true, applePodcast: false);
        var release = DomainTestFixture.UtcDateDaysAgo(2).AddHours(9);
        var report = Report(podcast, organisation, release, FullLength);
        _storedReports = [report];
        _spotifyShow = [SpotifyEpisode(_fixture.CreateSpotifyId(), release.AddDays(-20), FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass(DomainTestFixture.UtcDateDaysAgo(30)));

        // Assert
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up, real Apple matcher: when a report missing Apple matches a same-day Apple " +
        "episode of the same title and length, then the report is saved with that link, because the real enricher " +
        "and resolver must still fill a genuine gap (control for the Apple guard).")]
    public async Task real_apple_match_fills_a_genuine_gap()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(spotifyShow: false, applePodcast: true);
        var release = DomainTestFixture.UtcDateDaysAgo(2).AddHours(9);
        _storedReports = [Report(podcast, organisation, release, FullLength)];
        var appleEpisodeId = _fixture.CreateAppleId();
        _appleShow = [AppleEpisode(podcast, appleEpisodeId, release, FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass(DomainTestFixture.UtcDateDaysAgo(30)));

        // Assert
        _savedReports.Should().ContainSingle().Which.Services.Should().ContainKey(ServiceKeys.Apple);
    }

    [Fact(DisplayName =
        "Catalogue parent audio catch-up guard, real Apple matcher: when a same-day report of the same title released " +
        "just before the window already holds the show's Apple episode, then the report in the window is not given " +
        "that link, because one Apple episode must never be linked to two reports.")]
    public async Task real_apple_link_held_outside_window_is_not_reassigned()
    {
        // Arrange
        var (organisation, podcast) = SeedOrganisation(spotifyShow: false, applePodcast: true);
        var day = DomainTestFixture.UtcDateDaysAgo(2);
        var appleEpisodeId = _fixture.CreateAppleId();
        var holder = AppleHolder(podcast, organisation, day.AddHours(6), appleEpisodeId);
        var candidate = Report(podcast, organisation, day.AddHours(12), FullLength);
        _storedReports = [candidate];
        _holderReports = [holder];
        _appleShow = [AppleEpisode(podcast, appleEpisodeId, day.AddHours(6), FullLength)];

        // Act
        await Sut.UpdateParents([organisation.Id], OddHourPass(day.AddHours(9)));

        // Assert
        _reportReads.Should().Be(2);
        _savedReports.Should().BeEmpty();
        candidate.Services.Should().NotContainKey(ServiceKeys.Apple);
    }
}
