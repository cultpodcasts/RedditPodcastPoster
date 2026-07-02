using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class AppleEpisodeEnricherTests
{
    private const long C2CDelayTicks = -27216000000000;
    private const long ExpectedAppleEpisodeId = 1000775174947;

    [Fact]
    public async Task Enrich_WhenYouTubeFirstEpisodeMergedWithSpotify_AppliesAppleUrl()
    {
        var podcast = new Podcast
        {
            Id = Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200"),
            Name = "Cults to Consciousness",
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = C2CDelayTicks,
            SpotifyId = "6oTbi9wKZ2czCvSwBKxxoH",
            AppleId = 1635013492
        };
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var episode = new Episode
        {
            Id = Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362"),
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = TimeSpan.Parse("01:28:37"),
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4",
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri("https://open.spotify.com/episode/6O1Z1s7ca0PI8Gq1rdt3j4")
            }
        };
        var appleEpisode = new AppleEpisode(
            ExpectedAppleEpisodeId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc),
            TimeSpan.Parse("01:31:59"),
            new Uri(
                "https://podcasts.apple.com/us/podcast/i-confronted-my-abuser-30-years-later-everything-changed/id1635013492?i=1000775174947"),
            string.Empty,
            false);

        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            new CapturingAppleEpisodeResolver([appleEpisode]),
            NullLogger<AppleEpisodeEnricher>.Instance);

        var enrichmentContext = new EnrichmentContext();
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        episode.AppleId.Should().Be(ExpectedAppleEpisodeId);
        episode.Urls.Apple.Should().NotBeNull();
        episode.Urls.Apple!.ToString().Should().Contain(ExpectedAppleEpisodeId.ToString());
        episode.Release.Should().Be(youTubeRelease);
        enrichmentContext.AppleUrlUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task Enrich_WhenAppleReleaseSameDateWithTime_BackfillsMidnightRelease()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            Name = "Test Podcast",
            AppleId = 1635013492
        };
        var dateOnlyRelease = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var appleRelease = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc);
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = "Test episode",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = "spotify-id",
            Urls = new ServiceUrls { Spotify = new Uri("https://open.spotify.com/episode/spotify-id") }
        };
        var appleEpisode = new AppleEpisode(
            ExpectedAppleEpisodeId,
            "Test episode",
            appleRelease,
            TimeSpan.FromMinutes(45),
            new Uri($"https://podcasts.apple.com/us/podcast/test/id1635013492?i={ExpectedAppleEpisodeId}"),
            string.Empty,
            false);

        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            new CapturingAppleEpisodeResolver([appleEpisode]),
            NullLogger<AppleEpisodeEnricher>.Instance);

        var enrichmentContext = new EnrichmentContext();
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        episode.Release.Should().Be(appleRelease);
        enrichmentContext.ReleaseUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task Enrich_WhenAppleReleaseDifferentDate_DoesNotBackfillMidnightRelease()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            Name = "Test Podcast",
            AppleId = 1635013492
        };
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var appleRelease = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc);
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = "Test episode",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = "spotify-id",
            Urls = new ServiceUrls { Spotify = new Uri("https://open.spotify.com/episode/spotify-id") }
        };
        var appleEpisode = new AppleEpisode(
            ExpectedAppleEpisodeId,
            "Test episode",
            appleRelease,
            TimeSpan.FromMinutes(45),
            new Uri($"https://podcasts.apple.com/us/podcast/test/id1635013492?i={ExpectedAppleEpisodeId}"),
            string.Empty,
            false);

        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            new CapturingAppleEpisodeResolver([appleEpisode]),
            NullLogger<AppleEpisodeEnricher>.Instance);

        var enrichmentContext = new EnrichmentContext();
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        episode.Release.Should().Be(dateOnlyRelease);
        enrichmentContext.ReleaseUpdated.Should().BeFalse();
    }

    private sealed class StubApplePodcastEnricher : IApplePodcastEnricher
    {
        public Task AddId(Podcast podcast) => Task.CompletedTask;
    }

    private sealed class CapturingAppleEpisodeResolver(IEnumerable<AppleEpisode> episodes) : IAppleEpisodeResolver
    {
        public Task<AppleEpisode?> FindEpisode(
            FindAppleEpisodeRequest request,
            IndexingContext indexingContext,
            Func<AppleEpisode, bool>? reducer = null)
        {
            var matches = episodes.AsEnumerable();
            if (reducer != null)
            {
                matches = matches.Where(reducer);
            }

            return Task.FromResult(
                matches.FirstOrDefault(x => x.Id == ExpectedAppleEpisodeId) ?? matches.FirstOrDefault());
        }
    }
}
