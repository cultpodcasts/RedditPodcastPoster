using FluentAssertions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class SubmitResultFormattingRules
{
    [Fact(DisplayName =
        "A classified submit result names its content kind when no episode row was created, " +
        "so a film or news dry-run is visible in the log.")]
    public void classified_result_names_content_kind_without_an_episode_id()
    {
        // Arrange
        var result = new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Film,
            Rejected: true,
            RequiresCurator: true);

        // Act
        var text = result.ToString();

        // Assert
        text.Should().Contain("content-kind: 'Film'");
        text.Should().Contain("rejected: 'True'");
        text.Should().Contain("requires-curator: 'True'");
        text.Should().NotContain("episode-id");
    }
}
