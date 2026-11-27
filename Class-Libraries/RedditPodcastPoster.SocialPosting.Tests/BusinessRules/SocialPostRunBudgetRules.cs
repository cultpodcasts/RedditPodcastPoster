using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.SocialPosting;

namespace RedditPodcastPoster.SocialPosting.Tests.BusinessRules;

/// <summary>
/// Per-run post/failure budget used by Twitter and Bluesky posters: reserve before send,
/// sent posts and failures spend caps, skips spend neither, and Stop ends the run.
/// </summary>
public class SocialPostRunBudgetRules
{
    private readonly Fixture _fixture = new();

    private SocialPostRunBudget CreateBudget(int maxPosts = 2, int maxFailures = 3) =>
        new(maxPosts, maxFailures, _fixture.Create<string>(), NullLogger.Instance);

    [Fact(DisplayName =
        "When TryReserve is called under the post cap, then it returns true and does not increment Posts, because the slot is only claimed after a successful send.")]
    public void try_reserve_under_cap_allows_attempt_without_counting_a_post()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 2);

        // Act
        var reserved = budget.TryReserve();

        // Assert
        reserved.Should().BeTrue();
        budget.Posts.Should().Be(0);
        budget.Stopped.Should().BeFalse();
        budget.HasCapacity.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When RecordSent is called after a reserved attempt, then Posts increases and HasCapacity becomes false once the post cap is reached.")]
    public void record_sent_spends_the_post_budget()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 1);
        budget.TryReserve().Should().BeTrue();

        // Act
        budget.RecordSent();

        // Assert
        budget.Posts.Should().Be(1);
        budget.HasCapacity.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When RecordSkip is called, then Posts and Failures stay unchanged and the run can still reserve, because a resolved skip spends neither budget.")]
    public void record_skip_spends_neither_budget()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 1, maxFailures: 1);

        // Act
        budget.RecordSkip();

        // Assert
        budget.Posts.Should().Be(0);
        budget.Failures.Should().Be(0);
        budget.Stopped.Should().BeFalse();
        budget.TryReserve().Should().BeTrue();
    }

    [Fact(DisplayName =
        "When RecordFailure is called below the failure cap, then Failures increases and the run continues, because transient failures do not end the run until the threshold.")]
    public void record_failure_below_cap_continues_the_run()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 5, maxFailures: 3);

        // Act
        var continueRun = budget.RecordFailure();

        // Assert
        continueRun.Should().BeTrue();
        budget.Failures.Should().Be(1);
        budget.Stopped.Should().BeFalse();
        budget.HasCapacity.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When RecordFailure reaches the failure cap, then the run stops and further TryReserve calls return false, because the failure threshold ends the run.")]
    public void record_failure_at_cap_stops_the_run()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 5, maxFailures: 1);

        // Act
        var continueRun = budget.RecordFailure();

        // Assert
        continueRun.Should().BeFalse();
        budget.Failures.Should().Be(1);
        budget.Stopped.Should().BeTrue();
        budget.TryReserve().Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Stop is called, then the run is marked stopped without changing Posts or Failures, because a run-wide refusal is not a counted post or failure.")]
    public void stop_ends_the_run_without_spending_budgets()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 5, maxFailures: 5);

        // Act
        budget.Stop();

        // Assert
        budget.Stopped.Should().BeTrue();
        budget.Posts.Should().Be(0);
        budget.Failures.Should().Be(0);
        budget.HasCapacity.Should().BeFalse();
        budget.TryReserve().Should().BeFalse();
    }

    [Fact(DisplayName =
        "When MaxPosts sends have been recorded, then TryReserve returns false and marks Stopped, because the post cap is an expected end of a healthy run.")]
    public void try_reserve_at_post_cap_stops_without_allowing_another_send()
    {
        // Arrange
        var budget = CreateBudget(maxPosts: 2, maxFailures: 5);
        budget.TryReserve();
        budget.RecordSent();
        budget.TryReserve();
        budget.RecordSent();

        // Act
        var reserved = budget.TryReserve();

        // Assert
        reserved.Should().BeFalse();
        budget.Posts.Should().Be(2);
        budget.Stopped.Should().BeTrue();
        budget.HasCapacity.Should().BeFalse();
    }
}
