using AutoFixture;
using FluentAssertions;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Models.Discovery;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoverySubmissionRulesTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "The supervisor diff lists results the supervisor accepted that the submitter did not as added, and results the submitter accepted that the supervisor dropped as removed, because both are AI-teaching signals.")]
    public void diff_lists_added_and_removed()
    {
        // Arrange
        var kept = _fixture.Create<Guid>();
        var dropped = _fixture.Create<Guid>();
        var missed = _fixture.Create<Guid>();

        // Act
        var diff = DiscoverySubmissionRules.ComputeDiff([kept, dropped], [kept, missed, missed]);

        // Assert
        diff.Added.Should().Equal(missed);
        diff.Removed.Should().Equal(dropped);
    }

    [Fact(DisplayName =
        "When the supervisor's final set equals the submitter's, the diff is empty on both sides, because nothing was corrected.")]
    public void identical_sets_have_empty_diff()
    {
        // Arrange
        var ids = _fixture.CreateMany<Guid>(3).ToArray();

        // Act
        var diff = DiscoverySubmissionRules.ComputeDiff(ids, ids.Reverse());

        // Assert
        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a submission names its supervisors, a supervisor not in that list may not review it, because the supervisors claim restricts who supervises whom.")]
    public void unmapped_supervisor_cannot_review()
    {
        // Arrange
        var submission = Submission(_fixture.Create<string>());

        // Act
        var canReview = DiscoverySubmissionRules.CanReview(Supervisor(_fixture.Create<string>()), submission);

        // Assert
        canReview.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a submission names its supervisors, a listed supervisor may review it.")]
    public void mapped_supervisor_can_review()
    {
        // Arrange
        var supervisor = _fixture.Create<string>();
        var submission = Submission(supervisor);

        // Act
        var canReview = DiscoverySubmissionRules.CanReview(Supervisor(supervisor), submission);

        // Assert
        canReview.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An admin may review any submission even when it names other supervisors, because admin overrides the mapping.")]
    public void admin_overrides_mapping()
    {
        // Arrange
        var submission = Submission(_fixture.Create<string>());
        var admin = new SupervisionCaller(_fixture.Create<string>(), IsAdmin: true, IsSupervisor: false);

        // Act
        var canReview = DiscoverySubmissionRules.CanReview(admin, submission);

        // Assert
        canReview.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a submission has no supervisor mapping, any supervisor may review it.")]
    public void unmapped_submission_reviewable_by_any_supervisor()
    {
        // Arrange
        var submission = Submission();

        // Act
        var canReview = DiscoverySubmissionRules.CanReview(Supervisor(_fixture.Create<string>()), submission);

        // Assert
        canReview.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A supervisor may not review their own submission, because supervision needs a second person.")]
    public void supervisor_cannot_review_own_submission()
    {
        // Arrange
        var submission = Submission();

        // Act
        var canReview = DiscoverySubmissionRules.CanReview(Supervisor(submission.SubmitterSubject), submission);

        // Assert
        canReview.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A caller with no supervise or admin permission may view only their own submission, never review it.")]
    public void submitter_views_own_but_cannot_review()
    {
        // Arrange
        var submission = Submission();
        var submitter = new SupervisionCaller(submission.SubmitterSubject, false, false);

        // Act
        var canView = DiscoverySubmissionRules.CanView(submitter, submission);
        var canReview = DiscoverySubmissionRules.CanReview(submitter, submission);

        // Assert
        canView.Should().BeTrue();
        canReview.Should().BeFalse();
    }

    private static SupervisionCaller Supervisor(string subject) => new(subject, IsAdmin: false, IsSupervisor: true);

    private DiscoverySubmission Submission(params string[] supervisors) => new()
    {
        Id = _fixture.Create<Guid>(),
        Status = DiscoverySubmissionStatus.Pending,
        SubmitterSubject = _fixture.Create<string>(),
        SupervisorSubjects = supervisors.Length > 0 ? supervisors : null
    };
}
