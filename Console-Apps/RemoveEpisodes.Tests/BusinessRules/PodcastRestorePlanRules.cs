using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RemoveEpisodes.PodcastRestore;

namespace RemoveEpisodes.Tests.BusinessRules;

public class PodcastRestorePlanRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Restore podcast: when a removed podcast's episodes carry parentRemoved, then the plan selects the podcast " +
        "for un-remove and every episode for parentRemoved clearing, without mutating anything, because podcast removal stamped all of them.")]
    public void removed_podcast_clears_parent_removed_on_all_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var episodes = new[]
        {
            _fixture.CreateStoredEpisode(podcast, e => e.ParentRemoved = true),
            _fixture.CreateStoredEpisode(podcast, e => e.ParentRemoved = true)
        };

        // Act
        var plan = PodcastRestorePlan.Create(podcast, episodes);

        // Assert
        plan.PodcastNeedsUnremove.Should().BeTrue();
        plan.EpisodesToClearParentRemoved.Should().BeEquivalentTo(episodes);
        podcast.Removed.Should().BeTrue("building a plan must not mutate state (dry run)");
    }

    [Fact(DisplayName =
        "Restore podcast: when an episode was itself removed before the podcast was removed, then it stays " +
        "removed and is not re-indexed, because podcast removal never changes episode.removed.")]
    public void independently_removed_episode_stays_removed()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var live = _fixture.CreateStoredEpisode(podcast, e => e.ParentRemoved = true);
        var previouslyRemoved = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ParentRemoved = true;
            e.Removed = true;
        });

        // Act
        var plan = PodcastRestorePlan.Create(podcast, [live, previouslyRemoved]);

        // Assert
        plan.EpisodesToRepublish.Should().ContainSingle().Which.Id.Should().Be(live.Id);
        plan.EpisodesLeftRemoved.Should().ContainSingle().Which.Id.Should().Be(previouslyRemoved.Id);
        previouslyRemoved.Removed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Restore podcast: when the podcast is not removed and no episode carries parentRemoved, then the plan " +
        "only re-publishes live episodes, because re-running a restore must be idempotent.")]
    public void already_restored_podcast_is_idempotent()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = false);
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.ParentRemoved = false);

        // Act
        var plan = PodcastRestorePlan.Create(podcast, [episode]);

        // Assert
        plan.PodcastNeedsUnremove.Should().BeFalse();
        plan.EpisodesToClearParentRemoved.Should().BeEmpty();
        plan.EpisodesToRepublish.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }
}
