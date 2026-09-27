using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class SubmitResultFormattingRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A dry-run submit names its content kind and omits episode and playable ids, " +
        "so a classified film or news item is visible before anything is persisted.")]
    public void dry_run_names_content_kind_without_episode_or_playable_id()
    {
        // Arrange
        var result = new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Film);

        // Act
        var text = result.ToString();

        // Assert
        result.Episode.Should().BeNull();
        result.PlayableId.Should().BeNull();
        result.Rejected.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
        text.Should().Contain("episode-Result: 'Created'");
        text.Should().Contain($"content-kind: '{SubmitClassification.Film}'");
        text.Should().NotContain("episode-id");
        text.Should().NotContain("playable-id");
        text.Should().NotContain("rejected");
        text.Should().NotContain("requires-curator");
    }

    [Fact(DisplayName =
        "A persisted playable submit names its content kind and playable id, " +
        "so a saved film, TV episode, or news report is visible without an episode row.")]
    public void persisted_playable_names_content_kind_and_playable_id()
    {
        // Arrange
        var playableId = _fixture.CreateGuid();
        var result = new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            ContentKind: SubmitClassification.NewsReport,
            PlayableId: playableId);

        // Act
        var text = result.ToString();

        // Assert
        result.Episode.Should().BeNull();
        result.Rejected.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
        text.Should().Contain("episode-Result: 'Created'");
        text.Should().Contain($"content-kind: '{SubmitClassification.NewsReport}'");
        text.Should().Contain($"playable-id: '{playableId}'");
        text.Should().NotContain("episode-id");
        text.Should().NotContain("rejected");
        text.Should().NotContain("requires-curator");
    }

    [Fact(DisplayName =
        "A rejected submit stays None and records rejected, " +
        "so the log does not look like a created film or episode.")]
    public void rejected_submit_records_rejected_on_none_state()
    {
        // Arrange
        var result = new SubmitResult(
            SubmitResultState.None,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Episode,
            Rejected: true);

        // Act
        var text = result.ToString();

        // Assert
        result.Episode.Should().BeNull();
        result.PlayableId.Should().BeNull();
        result.RequiresCurator.Should().BeFalse();
        text.Should().Contain("episode-Result: 'None'");
        text.Should().Contain($"content-kind: '{SubmitClassification.Episode}'");
        text.Should().Contain("rejected: 'True'");
        text.Should().NotContain("requires-curator");
        text.Should().NotContain("episode-id");
        text.Should().NotContain("playable-id");
    }

    [Fact(DisplayName =
        "A curator hold stays None and records requires-curator, " +
        "so the log does not look like a created playable.")]
    public void curator_hold_records_requires_curator_on_none_state()
    {
        // Arrange
        var result = new SubmitResult(
            SubmitResultState.None,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Episode,
            RequiresCurator: true);

        // Act
        var text = result.ToString();

        // Assert
        result.Episode.Should().BeNull();
        result.PlayableId.Should().BeNull();
        result.Rejected.Should().BeFalse();
        text.Should().Contain("episode-Result: 'None'");
        text.Should().Contain($"content-kind: '{SubmitClassification.Episode}'");
        text.Should().Contain("requires-curator: 'True'");
        text.Should().NotContain("rejected");
        text.Should().NotContain("episode-id");
        text.Should().NotContain("playable-id");
    }
}
