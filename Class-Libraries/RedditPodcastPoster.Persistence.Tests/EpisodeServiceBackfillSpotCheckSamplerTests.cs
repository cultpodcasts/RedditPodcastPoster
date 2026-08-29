using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Episodes;
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeServiceBackfillSpotCheckSamplerTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When fewer candidates than the reservoir capacity are offered, the sampler keeps every candidate, because the spot-check should include all of them.")]
    public void reservoir_keeps_every_candidate_when_below_capacity()
    {
        // Arrange
        var sampler = new EpisodeServiceBackfillSpotCheckSampler(capacity: 1000, random: new Random(1));
        var patches = CreatePatches(3);

        // Act
        foreach (var patch in patches)
        {
            sampler.Offer(patch);
        }

        var snapshot = sampler.Snapshot();

        // Assert
        snapshot.Should().HaveCount(patches.Count);
        snapshot.Select(s => s.EpisodeId).Should().BeEquivalentTo(patches.Select(p => p.EpisodeId));
        snapshot.Select(s => s.PodcastId).Should().BeEquivalentTo(patches.Select(p => p.PodcastId));
    }

    [Fact(DisplayName =
        "When more candidates than capacity are offered, the reservoir size equals capacity and every kept id was offered, because Algorithm R samples without exceeding the requested size.")]
    public void reservoir_caps_at_capacity_and_only_keeps_offered_ids()
    {
        // Arrange
        const int capacity = 5;
        var sampler = new EpisodeServiceBackfillSpotCheckSampler(capacity, random: new Random(1));
        var patches = CreatePatches(40);

        // Act
        foreach (var patch in patches)
        {
            sampler.Offer(patch);
        }

        var snapshot = sampler.Snapshot();

        // Assert
        snapshot.Should().HaveCount(capacity);
        var offered = patches.Select(p => p.EpisodeId).ToHashSet();
        snapshot.Select(s => s.EpisodeId).Should().OnlyContain(id => offered.Contains(id));
        snapshot.Select(s => s.EpisodeId).Should().OnlyHaveUniqueItems();
        sampler.Seen.Should().Be(patches.Count);
    }

    private List<EpisodeServiceCatalogPatch> CreatePatches(int count)
    {
        var patches = new List<EpisodeServiceCatalogPatch>(count);
        for (var i = 0; i < count; i++)
        {
            patches.Add(new EpisodeServiceCatalogPatch(
                _fixture.CreateGuid(),
                _fixture.CreateGuid(),
                Services: null,
                Ids: null));
        }

        return patches;
    }
}
