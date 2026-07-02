using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class AppleEpisodeResolverTests
{
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
            NullLogger<AppleEpisodeResolver>.Instance);

        var result = await sut.FindEpisode(
            request,
            new IndexingContext(),
            y => Math.Abs((y.Release - lookupRelease).Ticks) < TimeSpan.FromDays(14).Ticks);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1000775078015);
    }

    [Fact]
    public async Task FindEpisode_WhenMembersFirstC2CAbuserEpisodeUsesCatalogueReleaseReducer_ReturnsMatch()
    {
        const long c2cDelayTicks = -27216000000000;
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = c2cDelayTicks,
            SpotifyId = "6oTbi9wKZ2czCvSwBKxxoH",
            AppleId = 1635013492
        };
        var episode = new Episode
        {
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            Length = TimeSpan.Parse("01:28:37"),
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4",
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri("https://open.spotify.com/episode/6O1Z1s7ca0PI8Gq1rdt3j4")
            }
        };
        var lookupRelease = EpisodeReleaseMatchTolerance.GetAudioReleaseForPlatformLookup(podcast, episode);
        var appleEpisodes = new[]
        {
            new AppleEpisode(
                1000775174947,
                "I Confronted My Abuser 30 Years Later… Everything Changed",
                new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc),
                TimeSpan.Parse("01:31:59"),
                new Uri(
                    "https://podcasts.apple.com/us/podcast/i-confronted-my-abuser-30-years-later-everything-changed/id1635013492?i=1000775174947"),
                string.Empty,
                false)
        };
        var request = FindAppleEpisodeRequestFactory.Create(podcast, episode);
        var ticks = EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, episode.Length);

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            NullLogger<AppleEpisodeResolver>.Instance);

        var result = await sut.FindEpisode(
            request,
            new IndexingContext(),
            y => request.Released.HasValue &&
                 EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches(
                     y.Release,
                     lookupRelease,
                     ticks,
                     podcast));

        result.Should().NotBeNull();
        result!.Id.Should().Be(1000775174947);
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
