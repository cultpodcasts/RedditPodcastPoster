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
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new StubSubjectMatcher(),
            EmptyTitleCasingProvider(),
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
            Release = lookupRelease
        };

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            matcher,
            new StubSubjectMatcher(),
            EmptyTitleCasingProvider(),
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
                         Release = y.Release,
                         AppleId = y.Id
                     },
                     podcast));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(appleEpisodeId);
        request.Released.Should().Be(lookupRelease);
    }

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
