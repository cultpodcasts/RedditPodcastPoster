using RedditPodcastPoster.Subjects.Matching;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.TitleCasing;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Factories;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.PodcastServices.Apple.Tests.Fakes;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class AppleEpisodeResolverTests
{
    private readonly DomainTestFixture _fixture = new();
    [Fact(DisplayName =
        "Incident (Aug 2026): when Apple enriches a YouTube-discovered episode, a sole similar-duration " +
        "catalogue row with a wholly different title must not match.")]
    public async Task FindEpisode_WhenYouTubeDiscoveredTitleDiffersButDurationAndReleaseAlign_ReturnsNull()
    {
        // Arrange - explicit disjoint titles (CreateTitle pairs can still clear fuzzy confidence)
        const string youTubeTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string appleTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var lookupRelease = DomainTestFixture.UtcAtTime(-1, new TimeSpan(7, 0, 12));
        var episodeLength = TimeSpan.FromMinutes(54) + TimeSpan.FromSeconds(30);
        var matchingAppleId = _fixture.CreateAppleId();
        var appleEpisodes = new[]
        {
            new AppleEpisode(
                matchingAppleId,
                appleTitle,
                lookupRelease.AddHours(-8),
                episodeLength + TimeSpan.FromMinutes(3),
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id{_fixture.CreateAppleId()}?i={matchingAppleId}"),
                string.Empty,
                false),
            new AppleEpisode(
                _fixture.CreateAppleId(),
                _fixture.CreateTitle(),
                DomainTestFixture.UtcDateDaysAgo(90),
                TimeSpan.FromMinutes(82),
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id{_fixture.CreateAppleId()}?i={_fixture.CreateAppleId()}"),
                string.Empty,
                false)
        };

        var request = new FindAppleEpisodeRequest(
            _fixture.CreateAppleId(),
            _fixture.CreateTitle(),
            null,
            youTubeTitle,
            lookupRelease,
            null,
            episodeLength,
            TimeSpan.FromHours(1),
            EnrichingYouTubeDiscoveredEpisode: true);

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            CatalogueTestServices.CreatePolicy(
                new CatalogueSubjectClassifier(new StubSubjectMatcher(), EmptyTitleCasingProvider()),
                platformMatcher: EpisodeDomainTestServices.CreatePlatformMatcher()),
            NullLogger<AppleEpisodeResolver>.Instance);

        // Act
        var result = await sut.FindEpisode(
            request,
            new IndexingContext(),
            y => Math.Abs((y.Release - lookupRelease).Ticks) < TimeSpan.FromDays(14).Ticks);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a YouTube release authority episode with negative publishing delay is merged with Spotify, " +
        "Apple resolver uses catalogue release reducer and returns a matching catalogue row.")]
    public async Task FindEpisode_WhenYouTubeReleaseAuthorityEpisodeUsesCatalogueReleaseReducer_ReturnsMatch()
    {
        // Arrange
        const int youTubeReleaseDaysAgo = 30;
        const int spotifyDaysAfterYouTube = 28;
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.AppleId = _fixture.CreateAppleId();
        var youTubeRelease = DomainTestFixture.UtcAtTime(
            -youTubeReleaseDaysAgo,
            _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = _fixture.CreateDuration();
        var storedTitle = _fixture.CreateShortTitle();
        var appleTitle = $"{storedTitle}: editorial Apple rename";
        var spotifyId = _fixture.CreateSpotifyId();
        var youTubeId = _fixture.CreateYouTubeId();
        var appleEpisodeId = _fixture.CreateAppleId();
        var appleCatalogueRelease = DomainTestFixture
            .SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, spotifyDaysAfterYouTube)
            .AddHours(8);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            spotifyId,
            youTubeId,
            youTubeRelease,
            storedLength,
            storedTitle);
        var lookupRelease = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);
        var appleEpisodes = new[]
        {
            new AppleEpisode(
                appleEpisodeId,
                appleTitle,
                appleCatalogueRelease,
                storedLength + TimeSpan.FromMinutes(3),
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id{podcast.AppleId}?i={appleEpisodeId}"),
                string.Empty,
                false)
        };
        var request = FindAppleEpisodeRequestFactory.Create(podcast, episode);
        var matcher = EpisodeDomainTestServices.CreatePlatformMatcher();
        var probeEpisode = new Episode
        {
            Title = episode.Title,
            Length = storedLength,
            ReleaseUtc = lookupRelease
        };

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            CatalogueTestServices.CreatePolicy(
                new CatalogueSubjectClassifier(new StubSubjectMatcher(), EmptyTitleCasingProvider()),
                platformMatcher: matcher),
            NullLogger<AppleEpisodeResolver>.Instance);

        // Act
        var result = await sut.FindEpisode(
            request,
            new IndexingContext(),
            y => request.Released.HasValue &&
                 matcher.CatalogueReleaseMatches(
                     probeEpisode,
                     new Episode
                     {
                         Title = y.Title,
                         Length = y.Duration,
                         ReleaseUtc = y.Release,
                         AppleId = y.Id
                     },
                     podcast));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(appleEpisodeId);
        request.Released.Should().Be(lookupRelease);
    }

    [Fact(DisplayName =
        "When a stored Apple episode id is not in the show feed, FindEpisode falls through to the shared catalogue match " +
        "instead of returning null, because a stale or invalid stored id must not block matching (same rule as Spotify).")]
    public async Task FindEpisode_KnownEpisodeIdMissing_FallsThroughToCatalogueMatch()
    {
        // Arrange
        const string title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var release = DomainTestFixture.UtcDateDaysAgo(2);
        var matchId = _fixture.CreateAppleId();
        var appleEpisodes = new[]
        {
            new AppleEpisode(matchId, title, release, TimeSpan.FromMinutes(44),
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id1?i={matchId}"), string.Empty, false)
        };
        var request = new FindAppleEpisodeRequest(
            _fixture.CreateAppleId(), _fixture.CreateTitle(), _fixture.CreateAppleId(), title,
            release, null, TimeSpan.FromMinutes(44), null);
        var sut = CreateSut(appleEpisodes);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchId);
    }

    [Fact(DisplayName =
        "When a recent Apple lookup has a weaker same-length candidate among the newest 50 and an exact-title candidate " +
        "further down the feed, FindEpisode returns the exact-title one, because Apple's cached feed is matched in a single pass.")]
    public async Task FindEpisode_RecentLookup_MatchesWholeFeedInOnePass()
    {
        // Arrange
        const string title = "Lantern Harbour Night Stories With A Guest From The Archive";
        var probeRelease = DateTime.UtcNow.AddHours(-56);
        var length = TimeSpan.FromMinutes(44);
        var exactId = _fixture.CreateAppleId();
        var weakerId = _fixture.CreateAppleId();
        var episodes = Enumerable.Range(1, 54).Select(i => new AppleEpisode(
                _fixture.CreateAppleId(), $"Unrelated Episode Number {i} About Volcanic Origami", DateTime.UtcNow.AddHours(-i),
                TimeSpan.FromMinutes(20), new Uri("https://podcasts.apple.com/us/podcast/episode/id1"), string.Empty, false))
            .Append(new AppleEpisode(weakerId, "Lantern Harbour Night Stories With A Guest", DateTime.UtcNow.AddHours(-1).AddMinutes(-30),
                length, new Uri($"https://podcasts.apple.com/us/podcast/episode/id1?i={weakerId}"), string.Empty, false))
            .Append(new AppleEpisode(exactId, title, probeRelease,
                length, new Uri($"https://podcasts.apple.com/us/podcast/episode/id1?i={exactId}"), string.Empty, false))
            .ToArray();
        var request = new FindAppleEpisodeRequest(
            _fixture.CreateAppleId(), _fixture.CreateTitle(), null, title,
            probeRelease, Service.YouTube, length, null);
        var sut = CreateSut(episodes);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(exactId);
    }

    private static AppleEpisodeResolver CreateSut(IEnumerable<AppleEpisode> appleEpisodes) =>
        new(
            new StubApplePodcastService(appleEpisodes),
            CatalogueTestServices.CreatePolicy(),
            NullLogger<AppleEpisodeResolver>.Instance);

    private sealed class StubApplePodcastService(IEnumerable<AppleEpisode> episodes) : ICachedApplePodcastService
    {
        public Task<AppleEpisode?> SingleUseGetEpisode(
            ApplePodcastId podcastId,
            long episodeId,
            IndexingContext indexingContext) =>
            GetEpisode(podcastId, episodeId, indexingContext);

        public Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId, IndexingContext indexingContext) =>
            Task.FromResult(episodes.FirstOrDefault(x => x.Id == episodeId));

        public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext) =>
            Task.FromResult<IEnumerable<AppleEpisode>?>(episodes);
    }
    private static IAsyncInstance<ITitleCasingRulesProvider> EmptyTitleCasingProvider() =>
        new StubAsyncInstance<ITitleCasingRulesProvider>(
            new TitleCasingRulesProvider(
                new Dictionary<string, TitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)));

    private sealed class StubAsyncInstance<T>(T value) : IAsyncInstance<T>
    {
        public Task<T> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }
}
