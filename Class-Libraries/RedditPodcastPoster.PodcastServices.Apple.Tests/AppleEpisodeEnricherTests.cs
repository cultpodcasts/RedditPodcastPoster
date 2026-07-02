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
    public async Task Enrich_WhenMembersFirstYouTubeEpisodeMergedWithSpotify_AppliesAppleUrl()
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
        var episode = new Episode
        {
            Id = Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362"),
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
        enrichmentContext.AppleUrlUpdated.Should().BeTrue();
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
