using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.Fakes;

public class CosmosLinqInMemoryRewriterRules
{
    private readonly DomainTestFixture _fixture = new();

    private sealed class PlayableStub
    {
        public Dictionary<string, ServiceLink>? Services { get; set; }
    }

    [Fact(DisplayName =
        "Cosmos LINQ in-memory rewriter: when Services lacks the Spotify key, then reading Services[spotify].Url " +
        "evaluates to null instead of throwing, because Cosmos treats a missing key as undefined (false).")]
    public void missing_service_key_is_undefined()
    {
        // Arrange
        var item = new PlayableStub
        {
            Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId()) }
            }
        };
        System.Linq.Expressions.Expression<Func<PlayableStub, bool>> predicate =
            x => x.Services![ServiceKeys.Spotify].Url != null;

        // Act
        var compiled = CosmosLinqInMemoryRewriter.ForInMemory(predicate).Compile();

        // Assert
        compiled(item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Cosmos LINQ in-memory rewriter: when Services is null, then reading Services[spotify].Url evaluates to " +
        "null instead of throwing, because Cosmos treats a null dictionary as undefined (false).")]
    public void null_services_dictionary_is_undefined()
    {
        // Arrange
        var item = new PlayableStub { Services = null };
        System.Linq.Expressions.Expression<Func<PlayableStub, bool>> predicate =
            x => x.Services![ServiceKeys.Spotify].Url != null;

        // Act
        var compiled = CosmosLinqInMemoryRewriter.ForInMemory(predicate).Compile();

        // Assert
        compiled(item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Cosmos LINQ in-memory rewriter: when Services holds the Spotify key with a url, then reading " +
        "Services[spotify].Url keeps that url, because a present key must still evaluate as in Cosmos.")]
    public void present_service_key_keeps_member_value()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var url = _fixture.DefaultSpotifyUrl(spotifyId);
        var item = new PlayableStub
        {
            Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
            {
                [ServiceKeys.Spotify] = new() { Url = url }
            }
        };
        System.Linq.Expressions.Expression<Func<PlayableStub, Uri?>> projection =
            x => x.Services![ServiceKeys.Spotify].Url;

        // Act
        var compiled = CosmosLinqInMemoryRewriter.ForInMemory(projection).Compile();

        // Assert
        compiled(item).Should().Be(url);
    }

    [Fact(DisplayName =
        "Cosmos LINQ in-memory rewriter: when PlayableCosmosFilters.HoldsSpotifyOrAppleLinkReleasedOnOrAfter is " +
        "compiled for a NewsReport that has Services but no Spotify or Apple key and ReleaseSort is on or after " +
        "since, then the predicate is false without throwing, because the production catch-up filter must treat " +
        "missing service keys as undefined the same way Cosmos does.")]
    public void production_holds_spotify_or_apple_filter_missing_keys_is_false()
    {
        // Arrange
        var since = DomainTestFixture.UtcDateDaysAgo(30);
        var item = new NewsReport(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId()) }
            }
        };
        item.SetRelease(CatalogueRelease.FromDateTimeUtc(DomainTestFixture.UtcDateDaysAgo(1)));
        var filter = PlayableCosmosFilters.HoldsSpotifyOrAppleLinkReleasedOnOrAfter<NewsReport>(since);

        // Act
        var compiled = CosmosLinqInMemoryRewriter.ForInMemory(filter).Compile();

        // Assert
        compiled(item).Should().BeFalse();
    }
}
