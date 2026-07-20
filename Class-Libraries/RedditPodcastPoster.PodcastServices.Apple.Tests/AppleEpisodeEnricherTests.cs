using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

/// <summary>
/// Legacy Apple enricher test entry point — catalogue E2E rules live in
/// <see cref="Enrichment.AppleEpisodeEnricherCatalogueRules"/>.
/// </summary>
public class AppleEpisodeEnricherTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a YouTube release authority episode with negative publishing delay is merged with Spotify, " +
        "enrichment applies Apple URL and preserves YouTube publish datetime.")]
    public async Task enrich_applies_apple_url_and_preserves_youtube_release_for_negative_delay_authority()
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
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            appleTitle,
            appleCatalogueRelease,
            storedLength + TimeSpan.FromMinutes(3),
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);

        var sut = CreateEnricher(
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));

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

    [Fact(DisplayName =
        "When Apple catalogue release shares the stored calendar date with a non-zero time, " +
        "the enricher backfills midnight UTC stored release to the Apple publish datetime.")]
    public async Task enrich_backfills_midnight_release_when_apple_release_shares_calendar_date()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());
        var dateOnlyRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var appleRelease = dateOnlyRelease.AddHours(8);
        var appleEpisodeId = _fixture.CreateAppleId();
        var spotifyId = _fixture.CreateSpotifyId();
        var sharedTitle = _fixture.CreateTitle();
        var sharedLength = _fixture.CreateDuration();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(sharedTitle)
            .WithRelease(dateOnlyRelease)
            .WithLength(sharedLength)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            sharedTitle,
            appleRelease,
            sharedLength,
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);

        var sut = CreateEnricher(
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));

        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.Release.Should().Be(appleRelease);
        enrichmentContext.ReleaseUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When Apple catalogue release is on a different calendar date, the enricher does not " +
        "backfill a midnight UTC stored release.")]
    public async Task enrich_does_not_backfill_release_when_apple_date_differs()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());
        var dateOnlyRelease = DomainTestFixture.UtcDateDaysAgo(3);
        var appleRelease = DomainTestFixture.UtcDateDaysAgo(2).AddHours(8);
        var appleEpisodeId = _fixture.CreateAppleId();
        var spotifyId = _fixture.CreateSpotifyId();
        var sharedTitle = _fixture.CreateTitle();
        var sharedLength = _fixture.CreateDuration();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(sharedTitle)
            .WithRelease(dateOnlyRelease)
            .WithLength(sharedLength)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            sharedTitle,
            appleRelease,
            sharedLength,
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);

        var sut = CreateEnricher(
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));

        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.Release.Should().Be(dateOnlyRelease);
        enrichmentContext.ReleaseUpdated.Should().BeFalse();
    }

    private static AppleEpisodeEnricher CreateEnricher(IAppleEpisodeResolver resolver) =>
        new(
            new StubApplePodcastEnricher(),
            resolver,
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new AppleEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            NullLogger<AppleEpisodeEnricher>.Instance);

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
