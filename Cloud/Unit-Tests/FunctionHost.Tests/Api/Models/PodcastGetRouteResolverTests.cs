using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

/// <summary>
/// Route shapes for GET podcast:
/// <list type="bullet">
/// <item><c>podcast/{podcastId}</c> — PodcastGet</item>
/// <item><c>podcast/{podcastName}</c> — PodcastGet (name may include ?)</item>
/// <item><c>podcast/{podcastName}/{episodeId}</c> — typed or PodcastGetSlash; name may include ? and/or /</item>
/// <item><c>podcast/{podcastId}/{episodeId}</c> — often PodcastGetSlash in prod; must resolve by PodcastId</item>
/// </list>
/// The trailing guid on two-segment routes is always an <b>episode</b> id (disambiguation), not a podcast id.
/// </summary>
public class PodcastGetRouteResolverTests
{
    public static TheoryData<string> PodcastNamesWithSpecialCharacters() => new()
    {
        "Was I In A Cult?",
        "True Crime Show w/ Guest Host",
        "Cult? Show w/ Nested Slash",
        "A/B Testing Podcast?"
    };

    [Fact(DisplayName =
        "PodcastGetSlash catch-all for podcastId/episodeId: continues as GetWithEpisodeId and handler request uses PodcastId, because App Insights shows PodcastGetSlash for curator guid/guid URLs and name lookup of a guid 404s.")]
    public void catch_all_podcast_id_slash_episode_id_resolves_by_podcast_id()
    {
        // Arrange
        var podcastId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var catchAllPath = $"{podcastId:D}/{episodeId:D}";

        // Act
        var resolution = PodcastGetRouteResolver.ForCatchAll(catchAllPath);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetSlash);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetWithEpisodeId);
        resolution.EpisodeRoutePodcastSegment.Should().Be(podcastId.ToString("D"));
        resolution.EpisodeRouteEpisodeId.Should().Be(episodeId);
        resolution.HandlerRequest.PodcastId.Should().Be(podcastId);
        resolution.HandlerRequest.PodcastName.Should().BeNull();
        resolution.HandlerRequest.EpisodeId.Should().BeNull();
        resolution.HandlerRequest.ToString().Should().Be($"PodcastId: '{podcastId}'.");
    }

    [Fact(DisplayName =
        "PodcastGetWithEpisodeId for podcastId/episodeId: handler request uses PodcastId, because the typed two-segment route must not treat a guid as a podcast name.")]
    public void typed_route_podcast_id_and_episode_id_resolves_by_podcast_id()
    {
        // Arrange
        var podcastId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();

        // Act
        var resolution = PodcastGetRouteResolver.ForNameAndEpisodeId(podcastId.ToString("D"), episodeId);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetWithEpisodeId);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetWithEpisodeId);
        resolution.HandlerRequest.PodcastId.Should().Be(podcastId);
        resolution.HandlerRequest.PodcastName.Should().BeNull();
    }

    [Theory(DisplayName =
        "PodcastGetSlash catch-all for podcast-name/episodeId when the name contains ? and/or /: continues as GetWithEpisodeId with PodcastName + EpisodeId (name lookup + episode disambiguation).")]
    [MemberData(nameof(PodcastNamesWithSpecialCharacters))]
    public void catch_all_podcast_name_slash_episode_id_keeps_name_lookup(string podcastName)
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var catchAllPath = $"{podcastName}/{episodeId:D}";

        // Act
        var resolution = PodcastGetRouteResolver.ForCatchAll(catchAllPath);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetSlash);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetWithEpisodeId);
        resolution.EpisodeRoutePodcastSegment.Should().Be(podcastName);
        resolution.EpisodeRouteEpisodeId.Should().Be(episodeId);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.EpisodeId.Should().Be(episodeId);
    }

    [Theory(DisplayName =
        "PodcastGetWithEpisodeId for podcast-name/episodeId when the name contains ? and/or /: handler request keeps PodcastName + EpisodeId.")]
    [MemberData(nameof(PodcastNamesWithSpecialCharacters))]
    public void typed_route_podcast_name_and_episode_id_keeps_name_lookup(string podcastName)
    {
        // Arrange
        var episodeId = Guid.NewGuid();

        // Act
        var resolution = PodcastGetRouteResolver.ForNameAndEpisodeId(podcastName, episodeId);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetWithEpisodeId);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetWithEpisodeId);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.EpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "PodcastGet single-segment podcast id: handler request uses PodcastId.")]
    public void single_segment_podcast_id_resolves_by_podcast_id()
    {
        // Arrange
        var podcastId = Guid.NewGuid();

        // Act
        var resolution = PodcastGetRouteResolver.ForSingleSegment(podcastId.ToString("D"));

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGet);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetByIdentifier);
        resolution.HandlerRequest.PodcastId.Should().Be(podcastId);
        resolution.HandlerRequest.PodcastName.Should().BeNull();
    }

    [Theory(DisplayName =
        "PodcastGet single-segment podcast name (including ? and /): handler request uses PodcastName.")]
    [MemberData(nameof(PodcastNamesWithSpecialCharacters))]
    public void single_segment_podcast_name_resolves_by_podcast_name(string podcastName)
    {
        // Arrange / Act
        var resolution = PodcastGetRouteResolver.ForSingleSegment(podcastName);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGet);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetByIdentifier);
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
        resolution.HandlerRequest.EpisodeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "PodcastGetSlash catch-all for slash-containing name without trailing episode id: continues as GetByIdentifier with the full path as the podcast name.")]
    public void catch_all_slash_name_without_trailing_episode_continues_as_by_identifier()
    {
        // Arrange
        const string podcastName = "True Crime Show w/ Guest Host";

        // Act
        var resolution = PodcastGetRouteResolver.ForCatchAll(podcastName);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetSlash);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetByIdentifier);
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
        resolution.EpisodeRouteEpisodeId.Should().BeNull();
    }
}
