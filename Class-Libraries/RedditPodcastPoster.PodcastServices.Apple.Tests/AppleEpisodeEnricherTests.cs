using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class AppleEpisodeEnricherTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When YouTube-first episode with negative publishing delay is merged with Spotify, " +
        "enrichment applies Apple URL and preserves YouTube publish datetime.")]
    public async Task Enrich_WhenYouTubeFirstEpisodeMergedWithSpotify_AppliesAppleUrl()
    {
        // Arrange
        const int youTubeReleaseDaysAgo = 30;
        const int spotifyDaysAfterYouTube = 28;
        var podcast = _fixture.CreateYouTubeFirstPodcastWithNegativeDelay();
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
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            appleTitle,
            appleCatalogueRelease,
            storedLength + TimeSpan.FromMinutes(3),
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);

        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId),
            NullLogger<AppleEpisodeEnricher>.Instance);

        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.AppleId.Should().Be(appleEpisodeId);
        episode.Urls.Apple.Should().NotBeNull();
        episode.Urls.Apple!.ToString().Should().Contain(appleEpisodeId.ToString());
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
            AppleId = _fixture.CreateAppleId()
        };
        var dateOnlyRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var appleRelease = dateOnlyRelease.AddHours(8);
        var appleEpisodeId = _fixture.CreateAppleId();
        var spotifyId = _fixture.CreateSpotifyId();
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = _fixture.CreateTitle(),
            Release = dateOnlyRelease,
            Length = _fixture.CreateDuration(),
            SpotifyId = spotifyId,
            Urls = new ServiceUrls { Spotify = _fixture.DefaultSpotifyUrl(spotifyId) }
        };
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            episode.Title,
            appleRelease,
            episode.Length,
            new Uri($"https://podcasts.apple.com/us/podcast/test/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);

        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId),
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
            AppleId = _fixture.CreateAppleId()
        };
        var dateOnlyRelease = DomainTestFixture.UtcDateDaysAgo(3);
        var appleRelease = DomainTestFixture.UtcDateDaysAgo(2).AddHours(8);
        var appleEpisodeId = _fixture.CreateAppleId();
        var spotifyId = _fixture.CreateSpotifyId();
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = _fixture.CreateTitle(),
            Release = dateOnlyRelease,
            Length = _fixture.CreateDuration(),
            SpotifyId = spotifyId,
            Urls = new ServiceUrls { Spotify = _fixture.DefaultSpotifyUrl(spotifyId) }
        };
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            episode.Title,
            appleRelease,
            episode.Length,
            new Uri($"https://podcasts.apple.com/us/podcast/test/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);

        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId),
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

    private sealed class CapturingAppleEpisodeResolver(
        IEnumerable<AppleEpisode> episodes,
        long expectedAppleEpisodeId) : IAppleEpisodeResolver
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
                matches.FirstOrDefault(x => x.Id == expectedAppleEpisodeId) ?? matches.FirstOrDefault());
        }
    }
}
