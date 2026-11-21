using System.Text;
using FluentAssertions;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Serialization;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

/// <summary>
/// #1415: the outputs "reviewed" flag was dropped. Submissions applied while it existed still carry
/// <c>outputsReviewPending</c> / <c>outputsReviewedAt</c> / <c>outputsReviewedBy</c> in Cosmos and must keep loading.
/// </summary>
public class DiscoverySubmissionLegacyReviewFieldsRules
{
    [Fact(DisplayName =
        "Legacy review fields (#1415): a stored submission that still has outputsReviewPending / outputsReviewedAt / outputsReviewedBy deserialises through the Cosmos serializer, and the rest of the document is read as before.")]
    public void stored_submission_with_dropped_review_fields_still_deserialises()
    {
        // Arrange
        var id = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var json = $$"""
            {
              "id": "{{id}}",
              "type": "DiscoverySubmission",
              "status": "Applied",
              "submitterSubject": "auth0|submitter",
              "submittedAt": "2026-11-21T14:00:00Z",
              "discoveryResultsDocumentIds": [],
              "submitterAcceptedResultIds": [],
              "itemOutcomes": [{ "resultId": "{{Guid.NewGuid()}}", "state": "Succeeded", "episodeId": "{{episodeId}}", "attempts": 1 }],
              "outputsReviewPending": true,
              "outputsReviewedAt": "2026-11-21T14:30:00Z",
              "outputsReviewedBy": "auth0|supervisor",
              "history": [{ "at": "2026-11-21T14:30:00Z", "actorSubject": "auth0|supervisor", "action": "OutputsReviewed" }],
              "_etag": "\"abc\""
            }
            """;
        var serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptionsProvider().GetJsonSerializerOptions());

        // Act
        var submission = serializer.FromStream<DiscoverySubmission>(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        // Assert
        submission.Id.Should().Be(id);
        submission.Status.Should().Be(DiscoverySubmissionStatus.Applied);
        submission.ItemOutcomes.Should().ContainSingle().Which.EpisodeId.Should().Be(episodeId);
        submission.History.Should().ContainSingle().Which.Action.Should().Be("OutputsReviewed");
        submission.ETag.Should().Be("\"abc\"");
    }
}
