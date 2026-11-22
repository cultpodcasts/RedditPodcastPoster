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

    [Fact(DisplayName = "Two different YouTube videos are planned as an additional YouTube link (RPP #1411, the Manic case).")]
    public void different_youtube_videos_become_an_alternate()
    {
        // Arrange
        var target = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=VCRf_qazO9s") };
        var source = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=GSIPAfDO_ec", "https://i.ytimg.com/vi/GSIPAfDO_ec/hqdefault.jpg") };

        // Act
        var plan = ServiceLinkMerge.Plan(target, source);

        // Assert
        plan.HasConflicts.Should().BeFalse();
        plan.ToAdd.Should().BeEmpty();
        plan.YouTubeAlternatesToAdd.Should().ContainSingle().Which.Url.Should().Be(new Uri("https://www.youtube.com/watch?v=GSIPAfDO_ec"));
        plan.HasWork.Should().BeTrue();
        ServiceLinkMerge.Apply(target, plan).Keys.Should().Equal(ServiceKeys.YouTube);
        var alternates = ServiceLinkMerge.ApplyYouTubeAlternates(null, plan, new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc));
        alternates.Should().ContainSingle().Which.YouTubeId.Should().Be("GSIPAfDO_ec");
    }

    [Fact(DisplayName = "A source YouTube that is already an alternate on the target is already held.")]
    public void youtube_already_held_as_alternate()
    {
        // Arrange
        var target = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = Link("https://www.youtube.com/watch?v=VCRf_qazO9s") };
        var source = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = Link("https://youtu.be/GSIPAfDO_ec") };
        var existing = new[]
        {
            new RedditPodcastPoster.Models.YouTube.YouTubeAlternate
            {
                YouTubeId = "GSIPAfDO_ec",
                Url = new Uri("https://www.youtube.com/watch?v=GSIPAfDO_ec"),
                AddedAt = DateTime.UtcNow
            }
        };

        // Act
        var plan = ServiceLinkMerge.Plan(target, source, existing);

        // Assert
        plan.HasConflicts.Should().BeFalse();
        plan.HasWork.Should().BeFalse();
        plan.AlreadyHeld.Should().Equal(ServiceKeys.YouTube);
    }

    [Fact(DisplayName = "Different Spotify (or Apple) links are still a conflict: those services stay single-valued.")]
    public void different_spotify_links_conflict()
    {
        // Arrange
        var target = new Dictionary<string, ServiceLink> { [ServiceKeys.Spotify] = Link("https://open.spotify.com/episode/aaa") };
        var source = new Dictionary<string, ServiceLink> { [ServiceKeys.Spotify] = Link("https://open.spotify.com/episode/bbb") };

        // Act
        var plan = ServiceLinkMerge.Plan(target, source);

        // Assert
        plan.HasConflicts.Should().BeTrue();
        plan.Conflicts.Should().ContainSingle().Which.Service.Should().Be(ServiceKeys.Spotify);
        plan.YouTubeAlternatesToAdd.Should().BeEmpty();
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
