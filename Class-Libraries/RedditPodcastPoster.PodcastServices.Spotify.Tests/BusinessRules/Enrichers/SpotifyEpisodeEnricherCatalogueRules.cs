using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Text.Sanitisers;

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
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
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
        "When no Spotify catalogue match is found, the enricher emits a Warning with episode-id " +
        "and rejection context so App Insights can explain enrich misses.")]
    public async Task enrich_logs_warning_with_rejection_context_when_no_catalogue_match()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Name = _fixture.CreateTitle();
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithDuration(TimeSpan.FromMinutes(58)));
        episode.Id = _fixture.CreateGuid();
        episode.Title = _fixture.CreateTitle();
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
        var logger = new CapturingLogger<SpotifyEpisodeEnricher>();
        var sut = CreateEnricher(
            new CapturingSpotifyEpisodeResolver([], expectedSpotifyId: string.Empty),
            logger);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        var miss = logger.Warnings.Should().ContainSingle(m => m.Contains("Spotify enrich miss")).Subject;
        miss.Should().Contain($"episode-id='{episode.Id}'");
        miss.Should().Contain($"podcast-id='{podcast.Id}'");
        miss.Should().Contain($"spotify-show-id='{podcast.SpotifyId}'");
        miss.Should().Contain("youtube-discovered=");
        miss.Should().Contain("expected-release=");
        miss.Should().Contain("length=");
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
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
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
        "When an audio-first podcast episode is still inside the delayed YouTube publishing window, " +
        "Spotify enrichment still queries the catalogue because Spotify audio is already live.")]
    public async Task enrich_queries_catalogue_for_audio_first_podcast_inside_delayed_youtube_window()
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
        EpisodeServicePresence.SetYouTubeIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, null, null);
        var resolver = new TrackingSpotifyEpisodeResolver();
        var sut = CreateEnricher(resolver);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        resolver.FindEpisodeInvoked.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast is still inside the delayed audio window, Spotify enrichment " +
        "is bypassed and does not query the catalogue.")]
    public async Task enrich_is_bypassed_for_youtube_authority_inside_delayed_audio_window()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks,
            _fixture.CreateSpotifyId());
        var inWindowRelease = DomainTestFixture.SpotifyCatalogueReleaseStillInsideDelayedPublishingWindow(
            publishingDelay);
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(inWindowRelease)
            .WithDuration(_fixture.CreateDuration()));
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
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

    private SpotifyEpisodeEnricher CreateEnricher(
        ISpotifyEpisodeResolver resolver,
        ILogger<SpotifyEpisodeEnricher>? logger = null) =>
        new(
            resolver,
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new SpotifyEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            new SpotifyExpensiveQuerySideEffect(NullLogger<SpotifyExpensiveQuerySideEffect>.Instance),
            _htmlSanitiser,
            logger ?? NullLogger<SpotifyEpisodeEnricher>.Instance);

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
            IsPlayable = true,
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
