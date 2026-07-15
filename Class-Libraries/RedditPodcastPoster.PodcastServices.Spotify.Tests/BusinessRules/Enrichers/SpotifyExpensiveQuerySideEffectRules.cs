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
/// Spotify expensive-query side-effect rules — podcast flag persistence after FindEpisode.
/// </summary>
public class SpotifyExpensiveQuerySideEffectRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When Spotify FindEpisode reports an expensive query, the side-effect sets " +
        "SpotifyEpisodesQueryIsExpensive on the podcast because the indexer must throttle future lookups.")]
    public async Task expensive_query_sets_podcast_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.SpotifyEpisodesQueryIsExpensive = false;
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            DomainTestFixture.UtcDaysAgo(5),
            _fixture.CreateDuration(),
            _fixture.CreateTitle());
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;
        var sut = CreateEnricher(isExpensiveQuery: true);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            new EnrichmentContext());

        // Assert
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When Spotify FindEpisode does not report an expensive query, the side-effect leaves " +
        "SpotifyEpisodesQueryIsExpensive unset because no throttling signal was returned.")]
    public async Task non_expensive_query_does_not_set_podcast_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.SpotifyEpisodesQueryIsExpensive = false;
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            DomainTestFixture.UtcDaysAgo(5),
            _fixture.CreateDuration(),
            _fixture.CreateTitle());
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;
        var sut = CreateEnricher(isExpensiveQuery: false);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            new EnrichmentContext());

        // Assert
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeFalse();
    }

    private SpotifyEpisodeEnricher CreateEnricher(bool isExpensiveQuery) =>
        new(
            new ExpensiveQueryOnlySpotifyEpisodeResolver(isExpensiveQuery),
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new SpotifyEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            new SpotifyExpensiveQuerySideEffect(),
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            NullLogger<SpotifyEpisodeEnricher>.Instance);

    private sealed class ExpensiveQueryOnlySpotifyEpisodeResolver(bool isExpensiveQuery) : ISpotifyEpisodeResolver
    {
        public Task<FindEpisodeResponse> FindEpisode(
            FindSpotifyEpisodeRequest request,
            IndexingContext indexingContext,
            Func<SimpleEpisode, bool>? reducer = null) =>
            Task.FromResult(new FindEpisodeResponse(FullEpisode: null, IsExpensiveQuery: isExpensiveQuery));
    }
}
