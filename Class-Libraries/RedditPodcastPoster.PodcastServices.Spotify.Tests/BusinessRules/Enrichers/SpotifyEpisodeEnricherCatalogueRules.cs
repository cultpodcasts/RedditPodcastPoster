using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Enrichers;

/// <summary>
/// Spotify episode enricher catalogue E2E rules mirroring YouTube enricher catalogue characterization.
/// </summary>
public class SpotifyEpisodeEnricherCatalogueRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IHtmlSanitiser _htmlSanitiser =
        new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance);

    [Fact(DisplayName =
        "When a YouTube-only stored episode matches a Spotify catalogue row, the enricher attaches " +
        "Spotify ID and URL via the domain applicator and marks the enrichment context.")]
    public async Task enrich_attaches_spotify_links_when_catalogue_match_found()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.SpotifyId = _fixture.CreateSpotifyId();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = _fixture.CreateDuration();
        var storedTitle = _fixture.CreateShortTitle();
        var spotifyId = _fixture.CreateSpotifyId();
        var alignedRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            28);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            youTubeRelease,
            storedLength,
            storedTitle);
        var fullEpisode = CreateFullEpisode(
            spotifyId,
            DomainTestFixture.CreateFuzzyTitleVariant(storedTitle),
            alignedRelease,
            storedLength + TimeSpan.FromMinutes(3));
        var sut = CreateEnricher(new CapturingSpotifyEpisodeResolver([fullEpisode], spotifyId));
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.SpotifyId.Should().Be(spotifyId);
        episode.Urls.Spotify.Should().NotBeNull();
        enrichmentContext.SpotifyUrlUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When no Spotify catalogue match is found, the enricher leaves the episode unchanged " +
        "and does not mark Spotify URL flags on the enrichment context.")]
    public async Task enrich_leaves_episode_unchanged_when_no_catalogue_match()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;
        var sut = CreateEnricher(new CapturingSpotifyEpisodeResolver([], expectedSpotifyId: string.Empty));
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.SpotifyId.Should().BeNullOrWhiteSpace();
        episode.Urls.Spotify.Should().BeNull();
        enrichmentContext.SpotifyUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the Spotify resolver reports an expensive query, the enricher side effect " +
        "marks the podcast Spotify episodes query as expensive.")]
    public async Task enrich_sets_expensive_query_flag_when_resolver_reports_expensive()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.SpotifyEpisodesQueryIsExpensive = null;
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithDuration(_fixture.CreateDuration()));
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;
        var sut = CreateEnricher(new ExpensiveQuerySpotifyEpisodeResolver());
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the episode is still inside the delayed YouTube publishing window, Spotify enrichment " +
        "is bypassed and does not query the catalogue.")]
    public async Task enrich_is_bypassed_inside_delayed_youtube_publishing_window()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var inWindowRelease = DomainTestFixture.SpotifyCatalogueReleaseStillInsideDelayedPublishingWindow(
            publishingDelay);
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(inWindowRelease)
            .WithDuration(_fixture.CreateDuration()));
        episode.YouTubeId = string.Empty;
        episode.Urls.YouTube = null;
        var resolver = new TrackingSpotifyEpisodeResolver();
        var sut = CreateEnricher(resolver);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        resolver.FindEpisodeInvoked.Should().BeFalse();
        enrichmentContext.SpotifyUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Spotify catalogue returns an episode id already owned by another stored episode, " +
        "Spotify enrichment leaves the current episode unchanged.")]
    public async Task enrich_skips_spotify_id_already_owned_by_another_episode()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.SpotifyId = _fixture.CreateSpotifyId();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = _fixture.CreateDuration();
        var storedTitle = _fixture.CreateShortTitle();
        var spotifyId = _fixture.CreateSpotifyId();
        var alignedRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            28);
        var current = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            youTubeRelease,
            storedLength,
            storedTitle);
        var other = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: alignedRelease,
            length: storedLength,
            title: _fixture.CreateTitle());
        other.SpotifyId = spotifyId;
        var fullEpisode = CreateFullEpisode(
            spotifyId,
            DomainTestFixture.CreateFuzzyTitleVariant(storedTitle),
            alignedRelease,
            storedLength + TimeSpan.FromMinutes(3));
        var sut = CreateEnricher(new CapturingSpotifyEpisodeResolver([fullEpisode], spotifyId));
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [current, other], current),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        current.SpotifyId.Should().BeNullOrWhiteSpace();
        enrichmentContext.SpotifyUrlUpdated.Should().BeFalse();
    }

    private SpotifyEpisodeEnricher CreateEnricher(ISpotifyEpisodeResolver resolver) =>
        new(
            resolver,
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new SpotifyEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            new SpotifyExpensiveQuerySideEffect(),
            _htmlSanitiser,
            NullLogger<SpotifyEpisodeEnricher>.Instance);

    private FullEpisode CreateFullEpisode(
        string spotifyId,
        string title,
        DateTime release,
        TimeSpan duration)
    {
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId).ToString();
        return new FullEpisode
        {
            Id = spotifyId,
            Name = title,
            HtmlDescription = $"<p>{_fixture.Create<string>()}</p>",
            DurationMs = (int)duration.TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = spotifyUrl },
            Images = []
        };
    }

    private sealed class CapturingSpotifyEpisodeResolver(
        IEnumerable<FullEpisode> episodes,
        string expectedSpotifyId) : ISpotifyEpisodeResolver
    {
        public Task<FindEpisodeResponse> FindEpisode(
            FindSpotifyEpisodeRequest request,
            IndexingContext indexingContext,
            Func<SimpleEpisode, bool>? reducer = null)
        {
            var candidates = episodes.Select(ToSimpleEpisode).AsEnumerable();
            if (reducer != null)
            {
                candidates = candidates.Where(reducer);
            }

            var match = string.IsNullOrWhiteSpace(expectedSpotifyId)
                ? candidates.FirstOrDefault()
                : candidates.FirstOrDefault(x => x.Id == expectedSpotifyId) ?? candidates.FirstOrDefault();
            var fullEpisode = match == null
                ? null
                : episodes.First(x => x.Id == match.Id);

            return Task.FromResult(new FindEpisodeResponse(fullEpisode));
        }

        private static SimpleEpisode ToSimpleEpisode(FullEpisode episode) =>
            new()
            {
                Id = episode.Id,
                Name = episode.Name,
                DurationMs = episode.DurationMs,
                ReleaseDate = episode.ReleaseDate,
                ExternalUrls = episode.ExternalUrls,
                Images = episode.Images
            };
    }

    private sealed class ExpensiveQuerySpotifyEpisodeResolver : ISpotifyEpisodeResolver
    {
        public Task<FindEpisodeResponse> FindEpisode(
            FindSpotifyEpisodeRequest request,
            IndexingContext indexingContext,
            Func<SimpleEpisode, bool>? reducer = null) =>
            Task.FromResult(new FindEpisodeResponse(null, IsExpensiveQuery: true));
    }

    private sealed class TrackingSpotifyEpisodeResolver : ISpotifyEpisodeResolver
    {
        public bool FindEpisodeInvoked { get; private set; }

        public Task<FindEpisodeResponse> FindEpisode(
            FindSpotifyEpisodeRequest request,
            IndexingContext indexingContext,
            Func<SimpleEpisode, bool>? reducer = null)
        {
            FindEpisodeInvoked = true;
            return Task.FromResult(new FindEpisodeResponse(null));
        }
    }
}
