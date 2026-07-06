using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class AppleEpisodeResolverTests
{
    private readonly DomainTestFixture _fixture = new();
    [Fact]
    public async Task FindEpisode_WhenYouTubeDiscoveredTitleDiffersButDurationAndReleaseAlign_ReturnsMatch()
    {
        var lookupRelease = new DateTime(2026, 7, 2, 7, 0, 12, DateTimeKind.Utc);
        var episodeLength = TimeSpan.FromMinutes(54) + TimeSpan.FromSeconds(30);
        var appleEpisodes = new[]
        {
            new AppleEpisode(
                1000775078015,
                "My Family Was America's Most Dangerous Cult",
                new DateTime(2026, 7, 1, 23, 0, 0, DateTimeKind.Utc),
                episodeLength + TimeSpan.FromMinutes(3),
                new Uri("https://podcasts.apple.com/us/podcast/my-family-was-americas-most-dangerous-cult/id1860966643?i=1000775078015"),
                string.Empty,
                false),
            new AppleEpisode(
                1000757443994,
                "Epstein's survivor: Jena-Lisa Jones Reveals all",
                new DateTime(2026, 3, 26, 8, 0, 11, DateTimeKind.Utc),
                TimeSpan.FromMinutes(82),
                new Uri("https://podcasts.apple.com/us/podcast/id1860966643?i=1000757443994"),
                string.Empty,
                false)
        };

        var request = new FindAppleEpisodeRequest(
            1860966643,
            "The Shadow Sessions Podcast",
            null,
            "\"I Grew Up in a Murder Cult\" Cult Survivor Reveals What It's Like To Grow Up Inside It",
            lookupRelease,
            null,
            episodeLength,
            TimeSpan.FromHours(1),
            EnrichingYouTubeDiscoveredEpisode: true);

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            NullLogger<AppleEpisodeResolver>.Instance);

        var result = await sut.FindEpisode(
            request,
            new IndexingContext(),
            y => Math.Abs((y.Release - lookupRelease).Ticks) < TimeSpan.FromDays(14).Ticks);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1000775078015);
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
        var appleTitle = DomainTestFixture.CreateFuzzyTitleVariant(storedTitle);
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
        var lookupRelease = EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);
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
        public void Flush()
        {
        }

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
}
