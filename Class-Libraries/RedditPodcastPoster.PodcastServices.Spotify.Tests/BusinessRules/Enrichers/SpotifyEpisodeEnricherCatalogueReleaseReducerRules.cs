using FluentAssertions;
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

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Enrichers;

/// <summary>
/// Spotify enricher business rules for catalogue release reducer delegation to the domain matcher.
/// </summary>
public class SpotifyEpisodeEnricherCatalogueReleaseReducerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IHtmlSanitiser _htmlSanitiser =
        new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance);

    [Fact(DisplayName =
        "When a YouTube release authority episode with negative publishing delay is enriched from Spotify, " +
        "the enricher filters catalogue candidates via CatalogueReleaseMatches and attaches the aligned row.")]
    public async Task enrich_filters_candidates_via_catalogue_release_reducer()
    {
        // Arrange
        const int youTubeReleaseDaysAgo = 30;
        const int spotifyDaysAfterYouTube = 28;
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(
            -youTubeReleaseDaysAgo,
            _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = _fixture.CreateDuration();
        var storedTitle = _fixture.CreateShortTitle();
        var alignedSpotifyId = _fixture.CreateSpotifyId();
        var misalignedSpotifyId = _fixture.CreateSpotifyId();
        var alignedRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            spotifyDaysAfterYouTube);
        var misalignedRelease = alignedRelease.AddDays(-30);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            youTubeRelease,
            storedLength,
            storedTitle);
        var alignedEpisode = CreateFullEpisode(
            alignedSpotifyId,
            DomainTestFixture.CreateFuzzyTitleVariant(storedTitle),
            alignedRelease,
            storedLength + TimeSpan.FromMinutes(3));
        var misalignedEpisode = CreateFullEpisode(
            misalignedSpotifyId,
            storedTitle,
            misalignedRelease,
            storedLength);

        var sut = new SpotifyEpisodeEnricher(
            new CapturingSpotifyEpisodeResolver([misalignedEpisode, alignedEpisode], alignedSpotifyId),
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new SpotifyEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            new SpotifyExpensiveQuerySideEffect(),
            _htmlSanitiser,
            NullLogger<SpotifyEpisodeEnricher>.Instance);

        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.SpotifyId.Should().Be(alignedSpotifyId);
        episode.Urls.Spotify.Should().NotBeNull();
        episode.Urls.Spotify!.ToString().Should().Contain(alignedSpotifyId);
        enrichmentContext.SpotifyUrlUpdated.Should().BeTrue();
    }

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

            var match = candidates.FirstOrDefault(x => x.Id == expectedSpotifyId)
                        ?? candidates.FirstOrDefault();
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
}
