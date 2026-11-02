using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

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
        System.Linq.Expressions.Expression<Func<PlayableStub, bool>> predicate =
            x => x.Services![ServiceKeys.Spotify].Url != null;

        // Act
        var compiled = CosmosLinqInMemoryRewriter.ForInMemory(predicate).Compile();

        // Assert
        compiled(item).Should().BeTrue();
    }
}
