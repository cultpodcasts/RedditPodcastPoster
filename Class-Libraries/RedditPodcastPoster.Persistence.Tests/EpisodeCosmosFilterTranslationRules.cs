using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeCosmosFilterTranslationRules
{
    private readonly DomainTestFixture _fixture = new();

    private CosmosClient CreateClient() =>
        new(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(_fixture.CreateGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });

    [Fact(DisplayName =
        "When episodes are filtered by ReleasedOnOrAfter, then the Cosmos SQL compares releaseSort only and never reads the legacy release fallback, because the GATE 5 bridge drop relies on every stored episode carrying releaseSort.")]
    public void released_on_or_after_translates_to_release_sort_only()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 30 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "episodes");

        // Act
        var sql = container.GetItemLinqQueryable<Episode>()
            .Where(EpisodeCosmosFilters.ReleasedOnOrAfter(since))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().NotContain("[\"release\"]");
        sql.Should().NotContain("IS_DEFINED");
    }

    [Fact(DisplayName =
        "When episodes are filtered by ParentNotRemoved, then the Cosmos SQL reads parentRemoved and never the retired podcastRemoved key, because the GATE 1 wire rewrite moved that flag to parentRemoved.")]
    public void parent_not_removed_translates_to_parent_removed_only()
    {
        // Arrange
        using var client = CreateClient();
        var container = client.GetContainer("db", "episodes");

        // Act
        var sql = container.GetItemLinqQueryable<Episode>()
            .Where(EpisodeCosmosFilters.ParentNotRemoved)
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"parentRemoved\"]");
        sql.Should().NotContain("podcastRemoved");
    }

    [Fact(DisplayName =
        "When TV show episodes are filtered by PlayableCosmosFilters.ReleasedOnOrAfter, then the Cosmos SQL compares releaseSort only, because new playable containers were always written with releaseSort.")]
    public void playable_released_on_or_after_translates_to_release_sort_only()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 30 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "tvShowEpisodes");

        // Act
        var sql = container.GetItemLinqQueryable<TvShowEpisode>()
            .Where(PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().NotContain("[\"release\"]");
    }

    [Fact(DisplayName =
        "When news reports are filtered by PlayableCosmosFilters.HoldsSpotifyOrAppleLinkReleasedOnOrAfter, then the Cosmos SQL compares releaseSort only and never reads the legacy release fallback, because the Spotify / Apple catch-up must not reintroduce the dual-key release read dropped in GATE 5.")]
    public void holds_spotify_or_apple_link_released_on_or_after_translates_to_release_sort_only()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 30 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "newsReports");

        // Act
        var sql = container.GetItemLinqQueryable<NewsReport>()
            .Where(PlayableCosmosFilters.HoldsSpotifyOrAppleLinkReleasedOnOrAfter<NewsReport>(since))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().NotContain("[\"release\"]");
        sql.Should().NotContain("IS_DEFINED");
    }

    [Fact(DisplayName =
        "When news reports are filtered by PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange for a Spotify " +
        "catch-up, then the Cosmos SQL compares releaseSort with a half-open upper bound and requires a missing Spotify " +
        "url, because the older same-title gap read must not pull in-window items or Spotify link holders.")]
    public void missing_audio_link_released_in_half_open_range_translates_to_release_sort_bounds_and_missing_spotify()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 20 + 10);
        var before = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 5 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "newsReports");

        // Act
        var sql = container.GetItemLinqQueryable<NewsReport>()
            .Where(PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange<NewsReport>(
                since, before, lookUpSpotify: true, lookUpApple: false))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().Contain("[\"releaseSort\"] <");
        sql.Should().Contain("spotify");
        sql.Should().NotContain("apple");
        sql.Should().NotContain("[\"release\"]");
        sql.Should().NotContain("IS_DEFINED");
    }

    [Fact(DisplayName =
        "MissingAudioLinkReleasedInHalfOpenRange for Spotify includes an older report that lacks Spotify, and " +
        "excludes an in-window missing-Spotify report and an older Spotify holder, because the catch-up older-gap " +
        "read is the half-open missing-audio slice only.")]
    public void missing_audio_link_half_open_range_includes_older_gap_and_excludes_window_and_holder()
    {
        // Arrange
        var since = DomainTestFixture.UtcDateDaysAgo(14);
        var before = DomainTestFixture.UtcDateDaysAgo(2);
        var podcast = _fixture.CreatePodcast();
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var olderGap = CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(8)), organisation);
        var inWindow = CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(1)), organisation);
        // Null Services short-circuits the missing-Spotify check without dictionary indexer access (Cosmos treats a
        // missing key as undefined; in-memory evaluation would throw on a YouTube-only Services map).
        olderGap.Services = null;
        inWindow.Services = null;
        var holderEpisode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(5));
        var spotifyId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(holderEpisode, spotifyId);
        EpisodeServicePresence.Upsert(holderEpisode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(spotifyId), null);
        var holder = CatalogueParentKindMapper.ToNewsReport(holderEpisode, organisation);
        var filter = PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange<NewsReport>(
            since, before, lookUpSpotify: true, lookUpApple: false);
        var predicate = CosmosLinqInMemoryRewriter.ForInMemory(filter).Compile();

        // Act
        var matched = new[] { olderGap, inWindow, holder }.Where(predicate).Select(x => x.Id).ToList();

        // Assert
        matched.Should().Equal(olderGap.Id);
    }
}
