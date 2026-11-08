using System.Linq.Expressions;
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

    /// <summary>
    /// Cosmos treats a missing service key as undefined, so <c>Services[key].Url != null</c> is false;
    /// in-memory dictionary access throws — guard member reads with <c>ContainsKey</c>.
    /// </summary>
    private static Func<NewsReport, bool> CompileForInMemory(Expression<Func<NewsReport, bool>> filter)
    {
        var cosmosFree = CosmosLinqInMemoryRewriter.ForInMemory(filter);
        return Expression.Lambda<Func<NewsReport, bool>>(
                new MissingKeyIsUndefined().Visit(cosmosFree.Body)!, cosmosFree.Parameters)
            .Compile();
    }

    private sealed class MissingKeyIsUndefined : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is MethodCallExpression { Method.Name: "get_Item", Object: { } dictionary } indexer &&
                dictionary.Type.IsGenericType &&
                dictionary.Type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var visited = (MemberExpression)base.VisitMember(node);
                var containsKey = Expression.Call(
                    Visit(dictionary), dictionary.Type.GetMethod("ContainsKey")!, Visit(indexer.Arguments[0]));
                return Expression.Condition(containsKey, visited, Expression.Default(node.Type));
            }

            return base.VisitMember(node);
        }
    }

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
        "When news reports are filtered by PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange for an Apple " +
        "catch-up, then the Cosmos SQL compares releaseSort with a half-open upper bound and requires a missing Apple " +
        "url, because the older same-title gap read must not pull in-window items or Apple link holders.")]
    public void missing_audio_link_released_in_half_open_range_translates_to_release_sort_bounds_and_missing_apple()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 20 + 10);
        var before = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 5 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "newsReports");

        // Act
        var sql = container.GetItemLinqQueryable<NewsReport>()
            .Where(PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange<NewsReport>(
                since, before, lookUpSpotify: false, lookUpApple: true))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().Contain("[\"releaseSort\"] <");
        sql.Should().Contain("apple");
        sql.Should().NotContain("spotify");
        sql.Should().NotContain("[\"release\"]");
        sql.Should().NotContain("IS_DEFINED");
    }

    [Fact(DisplayName =
        "When news reports are filtered by PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange for a dual " +
        "Spotify and Apple catch-up, then the Cosmos SQL compares releaseSort with a half-open upper bound and " +
        "mentions both spotify and apple, because dual-id parents must load gaps missing either service.")]
    public void missing_audio_link_released_in_half_open_range_translates_to_release_sort_bounds_and_missing_spotify_or_apple()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 20 + 10);
        var before = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 5 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "newsReports");

        // Act
        var sql = container.GetItemLinqQueryable<NewsReport>()
            .Where(PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange<NewsReport>(
                since, before, lookUpSpotify: true, lookUpApple: true))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().Contain("[\"releaseSort\"] <");
        sql.Should().Contain("spotify");
        sql.Should().Contain("apple");
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
        var predicate = CompileForInMemory(filter);

        // Act
        var matched = new[] { olderGap, inWindow, holder }.Where(predicate).Select(x => x.Id).ToList();

        // Assert
        matched.Should().Equal(olderGap.Id);
    }

    [Fact(DisplayName =
        "MissingAudioLinkReleasedInHalfOpenRange for dual Spotify and Apple includes older Spotify-only and " +
        "Apple-only gaps, and excludes a dual holder and an in-window missing-audio report, because dual-id parents " +
        "must surface either-service older gaps for same-title deferral.")]
    public void missing_audio_link_half_open_range_dual_includes_either_service_gap_and_excludes_dual_holder_and_window()
    {
        // Arrange
        var since = DomainTestFixture.UtcDateDaysAgo(14);
        var before = DomainTestFixture.UtcDateDaysAgo(2);
        var podcast = _fixture.CreatePodcast();
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        var spotifyOnlyGapEpisode =
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(8));
        var appleIdForSpotifyGap = _fixture.CreateAppleId();
        EpisodeServicePresence.SetAppleIdentity(spotifyOnlyGapEpisode, appleIdForSpotifyGap);
        EpisodeServicePresence.Upsert(
            spotifyOnlyGapEpisode, ServiceKeys.Apple, _fixture.DefaultAppleUrl(appleIdForSpotifyGap), null);
        var spotifyOnlyGap = CatalogueParentKindMapper.ToNewsReport(spotifyOnlyGapEpisode, organisation);

        var appleOnlyGapEpisode =
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(7));
        var spotifyIdForAppleGap = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(appleOnlyGapEpisode, spotifyIdForAppleGap);
        EpisodeServicePresence.Upsert(
            appleOnlyGapEpisode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(spotifyIdForAppleGap), null);
        var appleOnlyGap = CatalogueParentKindMapper.ToNewsReport(appleOnlyGapEpisode, organisation);

        var dualHolderEpisode =
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(6));
        var dualSpotifyId = _fixture.CreateSpotifyId();
        var dualAppleId = _fixture.CreateAppleId();
        EpisodeServicePresence.SetSpotifyIdentity(dualHolderEpisode, dualSpotifyId);
        EpisodeServicePresence.Upsert(
            dualHolderEpisode, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(dualSpotifyId), null);
        EpisodeServicePresence.SetAppleIdentity(dualHolderEpisode, dualAppleId);
        EpisodeServicePresence.Upsert(
            dualHolderEpisode, ServiceKeys.Apple, _fixture.DefaultAppleUrl(dualAppleId), null);
        var dualHolder = CatalogueParentKindMapper.ToNewsReport(dualHolderEpisode, organisation);

        var inWindow = CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, DomainTestFixture.UtcDateDaysAgo(1)), organisation);
        inWindow.Services = null;

        var filter = PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange<NewsReport>(
            since, before, lookUpSpotify: true, lookUpApple: true);
        var predicate = CompileForInMemory(filter);

        // Act
        var matched = new[] { spotifyOnlyGap, appleOnlyGap, dualHolder, inWindow }
            .Where(predicate)
            .Select(x => x.Id)
            .ToList();

        // Assert
        matched.Should().BeEquivalentTo([spotifyOnlyGap.Id, appleOnlyGap.Id]);
    }

    [Fact(DisplayName =
        "MissingAudioLinkReleasedInHalfOpenRange throws when neither Spotify nor Apple lookup is requested, " +
        "because a both-false call has no meaningful missing-audio predicate.")]
    public void missing_audio_link_half_open_range_throws_when_neither_lookup_requested()
    {
        // Arrange
        var since = DomainTestFixture.UtcDateDaysAgo(14);
        var before = DomainTestFixture.UtcDateDaysAgo(2);

        // Act
        var act = () => PlayableCosmosFilters.MissingAudioLinkReleasedInHalfOpenRange<NewsReport>(
            since, before, lookUpSpotify: false, lookUpApple: false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*lookUpSpotify*lookUpApple*");
    }
}
