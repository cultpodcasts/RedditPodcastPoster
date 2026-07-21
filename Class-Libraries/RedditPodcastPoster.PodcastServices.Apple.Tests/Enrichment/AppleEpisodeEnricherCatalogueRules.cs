using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.Enrichment;

/// <summary>
/// Apple episode enricher catalogue E2E rules mirroring YouTube enricher catalogue characterization.
/// </summary>
public class AppleEpisodeEnricherCatalogueRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a YouTube release authority episode with negative publishing delay is enriched from Apple, " +
        "the enricher applies Apple URL and preserves YouTube publish datetime.")]
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
        var sut = CreateEnricher(new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.AppleId.Should().Be(appleEpisodeId);
        episode.Urls.Apple.Should().NotBeNull();
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
        var sut = CreateEnricher(new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));
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
        var sut = CreateEnricher(new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));
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

    [Fact(DisplayName =
        "When the episode is still inside the delayed YouTube publishing window, Apple enrichment " +
        "is bypassed and does not query the catalogue.")]
    public async Task enrich_is_bypassed_inside_delayed_youtube_publishing_window()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var inWindowRelease = DomainTestFixture.SpotifyCatalogueReleaseStillInsideDelayedPublishingWindow(
            publishingDelay);
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(inWindowRelease)
            .WithLength(_fixture.CreateDuration())
            .Create();
        episode.AppleId = null;
        episode.Urls.Apple = null;
        var resolver = new TrackingAppleEpisodeResolver();
        var sut = CreateEnricher(resolver);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        resolver.FindEpisodeInvoked.Should().BeFalse();
        enrichmentContext.AppleUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When no Apple catalogue match is found, Apple enrichment leaves the episode unchanged " +
        "and does not mark Apple URL flags.")]
    public async Task enrich_leaves_episode_unchanged_when_no_catalogue_match()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.AppleId = null;
            e.Urls = new ServiceUrls();
        });
        var sut = CreateEnricher(new TrackingAppleEpisodeResolver());
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.AppleId.Should().BeNull();
        episode.Urls.Apple.Should().BeNull();
        enrichmentContext.AppleUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Apple catalogue returns an episode id already owned by another stored episode, " +
        "Apple enrichment leaves the current episode unchanged.")]
    public async Task enrich_skips_apple_id_already_owned_by_another_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());
        var appleEpisodeId = _fixture.CreateAppleId();
        var sharedTitle = _fixture.CreateTitle();
        var sharedLength = _fixture.CreateDuration();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var current = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(sharedTitle)
            .WithRelease(sharedRelease)
            .WithLength(sharedLength)
            .Create();
        current.AppleId = null;
        current.Urls.Apple = null;
        var other = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(_fixture.CreateTitle())
            .WithRelease(sharedRelease.AddDays(-1))
            .WithLength(sharedLength)
            .Create();
        other.AppleId = appleEpisodeId;
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            sharedTitle,
            sharedRelease.AddHours(8),
            sharedLength,
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{podcast.AppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);
        var sut = CreateEnricher(new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId));
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [current, other], current),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        current.AppleId.Should().BeNull();
        enrichmentContext.AppleUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the podcast has no Apple show id but the podcast enricher resolves one, " +
        "episode enrichment continues and applies a matching catalogue row.")]
    public async Task enrich_resolves_podcast_apple_id_then_applies_catalogue_match()
    {
        // Arrange
        var resolvedAppleId = _fixture.CreateAppleId();
        var podcast = _fixture.CreatePodcast(p => p.AppleId = null);
        var appleEpisodeId = _fixture.CreateAppleId();
        var sharedTitle = _fixture.CreateTitle();
        var sharedLength = _fixture.CreateDuration();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(sharedTitle)
            .WithRelease(sharedRelease)
            .WithLength(sharedLength)
            .WithSpotify(_fixture.CreateSpotifyId(), _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId()))
            .Create();
        episode.AppleId = null;
        episode.Urls.Apple = null;
        var appleEpisode = new AppleEpisode(
            appleEpisodeId,
            sharedTitle,
            sharedRelease.AddHours(8),
            sharedLength,
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{resolvedAppleId}?i={appleEpisodeId}"),
            string.Empty,
            false);
        var sut = new AppleEpisodeEnricher(
            new ResolvingApplePodcastEnricher(resolvedAppleId),
            new CapturingAppleEpisodeResolver([appleEpisode], appleEpisodeId),
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new AppleEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            NullLogger<AppleEpisodeEnricher>.Instance);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        podcast.AppleId.Should().Be(resolvedAppleId);
        episode.AppleId.Should().Be(appleEpisodeId);
        enrichmentContext.AppleUrlUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the podcast still has no Apple show id after podcast enricher runs, " +
        "episode enrichment exits without querying the catalogue.")]
    public async Task enrich_exits_when_podcast_apple_id_cannot_be_resolved()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = null);
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.AppleId = null;
            e.Urls = new ServiceUrls();
        });
        var resolver = new TrackingAppleEpisodeResolver();
        var sut = new AppleEpisodeEnricher(
            new StubApplePodcastEnricher(),
            resolver,
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new AppleEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            NullLogger<AppleEpisodeEnricher>.Instance);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        resolver.FindEpisodeInvoked.Should().BeFalse();
        enrichmentContext.AppleUrlUpdated.Should().BeFalse();
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

    private sealed class ResolvingApplePodcastEnricher(long appleId) : IApplePodcastEnricher
    {
        public Task AddId(Podcast podcast)
        {
            podcast.AppleId = appleId;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingAppleEpisodeResolver : IAppleEpisodeResolver
    {
        public bool FindEpisodeInvoked { get; private set; }

        public Task<AppleEpisode?> FindEpisode(
            FindAppleEpisodeRequest request,
            IndexingContext indexingContext,
            Func<AppleEpisode, bool>? reducer = null)
        {
            FindEpisodeInvoked = true;
            return Task.FromResult<AppleEpisode?>(null);
        }
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
