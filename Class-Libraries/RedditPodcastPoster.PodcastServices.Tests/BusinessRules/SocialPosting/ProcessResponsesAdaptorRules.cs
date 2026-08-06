using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.SocialPosting.Adaptors;
using RedditPodcastPoster.SocialPosting.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.SocialPosting;

public class ProcessResponsesAdaptorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Process response aggregation: when any posting result failed, then the aggregate is a failure that includes failure messages, because Poster must surface Reddit post failures.")]
    public void any_failure_makes_aggregate_fail()
    {
        // Arrange
        var failureMessage = _fixture.Create<string>();
        var successMessage = _fixture.Create<string>();
        var sut = new ProcessResponsesAdaptor(NullLogger<ProcessResponsesAdaptor>.Instance);
        var responses = new List<ProcessResponse>
        {
            ProcessResponse.Successful(successMessage),
            ProcessResponse.Fail(failureMessage)
        };

        // Act
        var aggregate = sut.CreateResponse(responses);

        // Assert
        aggregate.Success.Should().BeFalse();
        aggregate.Message.Should().Contain(failureMessage);
    }

    [Fact(DisplayName =
        "Process response aggregation: when all posting results succeeded, then the aggregate is successful, because a clean Reddit posting pass must not be reported as failure.")]
    public void all_success_makes_aggregate_succeed()
    {
        // Arrange
        var message = _fixture.Create<string>();
        var sut = new ProcessResponsesAdaptor(NullLogger<ProcessResponsesAdaptor>.Instance);
        var responses = new List<ProcessResponse>
        {
            ProcessResponse.Successful(message),
            ProcessResponse.Successful()
        };

        // Act
        var aggregate = sut.CreateResponse(responses);

        // Assert
        aggregate.Success.Should().BeTrue();
        aggregate.Message.Should().Contain(message);
    }
}
