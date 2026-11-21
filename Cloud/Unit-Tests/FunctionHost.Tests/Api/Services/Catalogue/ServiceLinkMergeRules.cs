using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>The shared service-link merge (RPP #1522; for reuse by #1471 merge episodes and the #1416 split).</summary>
public class ServiceLinkMergeRules
{
    private static ServiceLink Link(string url, string? image = null) =>
        new() { Url = new Uri(url), Image = image is null ? null : new Uri(image) };

    [Fact(DisplayName = "A service the target has no link for is planned as an addition, copying url and image.")]
    public void missing_service_is_added()
    {
        // Arrange
        var target = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = Link("https://open.spotify.com/episode/abc")
        };
        var source = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=GSIPAfDO_ec", "https://i.ytimg.com/vi/x.jpg")
        };

        // Act
        var plan = ServiceLinkMerge.Plan(target, source);
        var merged = ServiceLinkMerge.Apply(target, plan);

        // Assert
        plan.HasConflicts.Should().BeFalse();
        plan.ToAdd.Keys.Should().Equal(ServiceKeys.YouTube);
        merged.Keys.Should().BeEquivalentTo([ServiceKeys.Spotify, ServiceKeys.YouTube]);
        merged[ServiceKeys.YouTube].Image.Should().Be(new Uri("https://i.ytimg.com/vi/x.jpg"));
        target.Should().ContainSingle("Apply returns a new map and leaves the target as it was");
    }

    [Fact(DisplayName = "Two different YouTube videos for one item are a conflict (the Manic case until #1411).")]
    public void different_youtube_videos_conflict()
    {
        // Arrange
        var target = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=VCRf_qazO9s") };
        var source = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=GSIPAfDO_ec") };

        // Act
        var plan = ServiceLinkMerge.Plan(target, source);

        // Assert
        plan.HasConflicts.Should().BeTrue();
        plan.Conflicts.Should().ContainSingle().Which.Service.Should().Be(ServiceKeys.YouTube);
        plan.ToAdd.Should().BeEmpty();
    }

    [Theory(DisplayName = "The same link written differently is already held, not a conflict.")]
    [InlineData(ServiceKeys.YouTube, "https://www.youtube.com/watch?v=GSIPAfDO_ec", "https://youtu.be/GSIPAfDO_ec")]
    [InlineData(ServiceKeys.Spotify, "https://open.spotify.com/episode/abc/", "https://OPEN.spotify.com/episode/abc")]
    public void same_link_is_already_held(string service, string a, string b)
    {
        // Act
        var plan = ServiceLinkMerge.Plan(
            new Dictionary<string, ServiceLink> { [service] = Link(a) },
            new Dictionary<string, ServiceLink> { [service] = Link(b) });

        // Assert
        plan.HasConflicts.Should().BeFalse();
        plan.ToAdd.Should().BeEmpty();
        plan.AlreadyHeld.Should().Equal(service);
    }

    [Fact(DisplayName = "Null entries and links without a url are ignored on both sides, and a null target takes every source link.")]
    public void null_entries_are_ignored()
    {
        // Arrange
        var source = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Apple] = null!,
            [ServiceKeys.Spotify] = new() { Image = new Uri("https://example.com/i.jpg") },
            [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=GSIPAfDO_ec")
        };
        var target = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = null! };

        // Act
        var plan = ServiceLinkMerge.Plan(target, source);
        var fromNull = ServiceLinkMerge.Plan(null, source);

        // Assert
        plan.ToAdd.Keys.Should().Equal(ServiceKeys.YouTube);
        fromNull.ToAdd.Keys.Should().Equal(ServiceKeys.YouTube);
        ServiceLinkMerge.Apply(target, plan).Keys.Should().Equal(ServiceKeys.YouTube);
    }
}
