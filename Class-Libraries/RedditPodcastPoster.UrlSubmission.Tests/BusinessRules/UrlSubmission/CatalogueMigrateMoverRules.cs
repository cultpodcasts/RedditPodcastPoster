using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueMigrateMoverRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A News move plan keeps the podcast id as the organisation id and each episode id as a report id, " +
        "and it requires an allowlist before apply.")]
    public void news_plan_keeps_guids_and_requires_allowlist()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var plan = CatalogueMigrateMover.Plan(podcast, [episode], SubmitClassification.NewsReport);

        // Assert
        plan.Accepted.Should().BeTrue();
        plan.DestParentId.Should().Be(podcast.Id);
        plan.DestPlayableIds.Should().Equal(episode.Id);
        plan.RequiresAllowlist.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A TV move plan keeps the podcast id as the TV show id and each episode id as a TV-show episode id.")]
    public void tv_plan_keeps_guids()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var plan = CatalogueMigrateMover.Plan(podcast, [episode], SubmitClassification.TvShowEpisode);

        // Assert
        plan.Accepted.Should().BeTrue();
        plan.DestParentId.Should().Be(podcast.Id);
        plan.DestPlayableIds.Should().Equal(episode.Id);
        plan.RequiresAllowlist.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Film move plan for a single episode uses the episode id as the Film id and has no parent id, " +
        "because Film is a one-off.")]
    public void film_plan_uses_episode_id_and_no_parent()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var plan = CatalogueMigrateMover.Plan(podcast, [episode], SubmitClassification.Film);

        // Assert
        plan.Accepted.Should().BeTrue();
        plan.DestParentId.Should().BeNull();
        plan.DestPlayableIds.Should().Equal(episode.Id);
        plan.SourcePodcastId.Should().Be(podcast.Id);
        episode.Id.Should().NotBe(podcast.Id);
    }

    [Fact(DisplayName =
        "A Film move plan is rejected when there is not exactly one active episode, " +
        "because a series is a TvShow and never a Film.")]
    public void film_plan_rejects_multiple_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);

        // Act
        var plan = CatalogueMigrateMover.Plan(podcast, [first, second], SubmitClassification.Film);

        // Assert
        plan.Accepted.Should().BeFalse();
        plan.DestPlayableIds.Should().BeEmpty();
        plan.RejectReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "A Film move plan ignores a removed extra episode and accepts the remaining one-off.")]
    public void film_plan_ignores_removed_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var active = _fixture.CreateStoredEpisode(podcast);
        var removed = _fixture.CreateStoredEpisode(podcast, e => e.Removed = true);

        // Act
        var plan = CatalogueMigrateMover.Plan(podcast, [active, removed], SubmitClassification.Film);

        // Assert
        plan.Accepted.Should().BeTrue();
        plan.DestPlayableIds.Should().Equal(active.Id);
    }
}
