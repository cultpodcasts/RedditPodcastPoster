using Api.Models;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastGetRouteResolverTests
{
    [Fact(DisplayName =
        "PodcastGetSlash catch-all for podcastGuid/episodeGuid: continues as GetWithEpisodeId and handler request uses PodcastId, because App Insights shows PodcastGetSlash for curator guid/guid URLs and name lookup of a guid 404s.")]
    public void catch_all_guid_slash_guid_resolves_by_podcast_id()
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
        "PodcastGetWithEpisodeId for podcastGuid/episodeGuid: handler request uses PodcastId, because the typed two-segment route must not treat a guid as a podcast name.")]
    public void typed_route_guid_and_episode_resolves_by_podcast_id()
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

    [Fact(DisplayName =
        "PodcastGetSlash catch-all for name?/episodeGuid: continues as GetWithEpisodeId with PodcastName + EpisodeId for disambiguation.")]
    public void catch_all_question_mark_name_and_episode_keeps_name_lookup()
    {
        // Arrange
        const string podcastName = "Was I In A Cult?";
        var episodeId = Guid.NewGuid();
        var catchAllPath = $"{podcastName}/{episodeId:D}";

        // Act
        var resolution = PodcastGetRouteResolver.ForCatchAll(catchAllPath);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetSlash);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetWithEpisodeId);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.EpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "PodcastGetWithEpisodeId for name?/episodeGuid: handler request keeps PodcastName + EpisodeId.")]
    public void typed_route_question_mark_name_and_episode_keeps_name_lookup()
    {
        // Arrange
        const string podcastName = "Was I In A Cult?";
        var episodeId = Guid.NewGuid();

        // Act
        var resolution = PodcastGetRouteResolver.ForNameAndEpisodeId(podcastName, episodeId);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetWithEpisodeId);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.EpisodeId.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "PodcastGetSlash catch-all for slash-containing name/episodeGuid: continues as GetWithEpisodeId with the full name and episode id.")]
    public void catch_all_slash_containing_name_and_episode_keeps_name_lookup()
    {
        // Arrange
        const string podcastName = "True Crime Show w/ Guest Host";
        var episodeId = Guid.NewGuid();
        var catchAllPath = $"{podcastName}/{episodeId:D}";

        // Act
        var resolution = PodcastGetRouteResolver.ForCatchAll(catchAllPath);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetSlash);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetWithEpisodeId);
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.EpisodeId.Should().Be(episodeId);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
    }

    [Fact(DisplayName =
        "PodcastGet single-segment guid: handler request uses PodcastId.")]
    public void single_segment_guid_resolves_by_podcast_id()
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

    [Fact(DisplayName =
        "PodcastGet single-segment name: handler request uses PodcastName.")]
    public void single_segment_name_resolves_by_podcast_name()
    {
        // Arrange
        const string podcastName = "Was I In A Cult?";

        // Act
        var resolution = PodcastGetRouteResolver.ForSingleSegment(podcastName);

        // Assert
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGet);
        resolution.ContinuesAs.Should().Be(PodcastGetContinuation.GetByIdentifier);
        resolution.HandlerRequest.PodcastName.Should().Be(podcastName);
        resolution.HandlerRequest.PodcastId.Should().BeNull();
    }

    [Fact(DisplayName =
        "PodcastGetSlash catch-all without trailing episode guid: continues as GetByIdentifier with the full path as the identifier.")]
    public void catch_all_without_trailing_episode_continues_as_by_identifier()
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
