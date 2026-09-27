using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules;

public class SubmitUrlIndexableEpisodeIdRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a Created submit result has no episode and no playable id, it contributes no index id, " +
        "because a film or news dry-run must not be passed to IndexEpisodes.")]
    public void created_without_episode_contributes_no_id()
    {
        // Arrange
        var result = new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Film);

        // Act
        var episodeId = SubmitUrlIndexableEpisodeId.From(result);

        // Assert
        result.Episode.Should().BeNull();
        result.PlayableId.Should().BeNull();
        episodeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a Created submit result holds a podcast episode, it contributes that episode id, " +
        "so search indexing still updates the row that was written.")]
    public void created_podcast_episode_contributes_its_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var result = new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            Episode: episode);

        // Act
        var episodeId = SubmitUrlIndexableEpisodeId.From(result);

        // Assert
        episodeId.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When an Enriched submit result holds a podcast episode, it contributes that episode id, " +
        "because enrichment is the other write that search indexing must pick up.")]
    public void enriched_podcast_episode_contributes_its_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var result = new SubmitResult(
            SubmitResultState.Enriched,
            SubmitResultState.None,
            Episode: episode);

        // Act
        var episodeId = SubmitUrlIndexableEpisodeId.From(result);

        // Assert
        episodeId.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When a submit result holds a podcast episode but did not create or enrich it, it contributes no index id, " +
        "because an already-known row is not a new indexing target.")]
    public void episode_that_was_not_created_or_enriched_contributes_no_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var result = new SubmitResult(
            SubmitResultState.EpisodeAlreadyExists,
            SubmitResultState.None,
            Episode: episode);

        // Act
        var episodeId = SubmitUrlIndexableEpisodeId.From(result);

        // Assert
        episodeId.Should().BeNull();
    }
}
