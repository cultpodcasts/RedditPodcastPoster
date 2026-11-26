using Api.Models;
using Api.Services.Episodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

public class EpisodeAwaitingApprovalServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public EpisodeAwaitingApprovalServiceRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
    }

    [Fact(DisplayName =
        "When a curator lists split-offs awaiting approval, then only episodes whose approval status is awaiting approval are returned, newest release first, including one that is also removed, because the list queries approval status and not removed plus a tweet-skip reason (#1491).")]
    public async Task awaiting_approval_list_is_status_only_and_newest_first()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var newer = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ApprovalStatus = ApprovalStatus.AwaitingApproval;
            e.Removed = true;
            e.ReleaseUtc = DomainTestFixture.UtcDaysAgo(1);
        });
        var older = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ApprovalStatus = ApprovalStatus.AwaitingApproval;
            e.ReleaseUtc = DomainTestFixture.UtcDaysAgo(20);
        });
        var rejected = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ApprovalStatus = ApprovalStatus.Rejected;
            e.Removed = true;
        });
        var approved = _fixture.CreateStoredEpisode(podcast);
        _episodes.Seed(older, newer, rejected, approved);
        var sut = _mocker.CreateInstance<EpisodeAwaitingApprovalService>();

        // Act
        var result = await sut.GetAwaitingApprovalAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeAwaitingApprovalStatus.Ok);
        result.Episodes.Should().NotBeNull();
        result.Episodes!.Select(p => p.Episode.Id).Should().Equal(newer.Id, older.Id);
        result.Episodes.Should().OnlyContain(p =>
            p.Episode.IsAwaitingApproval() && p.Podcast.Id == podcast.Id && p.Podcast.Name == podcast.Name);
    }

    [Fact(DisplayName =
        "When an episode awaiting approval has no stored podcast, then it is not listed and the miss is logged with the episode and podcast ids, because the editor GET and the approve/reject PATCH both fail without a stored podcast so an orphan row could not be actioned (#1491).")]
    public async Task awaiting_approval_episode_with_no_stored_podcast_is_skipped_and_logged()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var listed = _fixture.CreateStoredEpisode(podcast, e => e.ApprovalStatus = ApprovalStatus.AwaitingApproval);
        var orphanPodcast = _fixture.CreatePodcast();
        var orphan = _fixture.CreateStoredEpisode(orphanPodcast, e => e.ApprovalStatus = ApprovalStatus.AwaitingApproval);
        _episodes.Seed(listed, orphan);
        var sut = _mocker.CreateInstance<EpisodeAwaitingApprovalService>();

        // Act
        var result = await sut.GetAwaitingApprovalAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeAwaitingApprovalStatus.Ok);
        result.Episodes.Should().NotBeNull();
        result.Episodes!.Select(p => p.Episode.Id).Should().Equal(listed.Id);
        VerifyLogged(LogLevel.Warning, Times.Once, orphan.Id.ToString(), orphanPodcast.Id.ToString());
    }

    [Fact(DisplayName =
        "When more episodes are awaiting approval than the expected maximum, then the count is logged at Warning, because held split-offs are a small set and a runaway set must show in App Insights (#1491).")]
    public async Task awaiting_approval_count_above_expected_maximum_is_logged_at_warning()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var held = Enumerable.Range(0, EpisodeAwaitingApprovalService.ExpectedMaximumAwaitingApproval + 1)
            .Select(_ => _fixture.CreateStoredEpisode(podcast, e => e.ApprovalStatus = ApprovalStatus.AwaitingApproval))
            .ToArray();
        _episodes.Seed(held);
        var sut = _mocker.CreateInstance<EpisodeAwaitingApprovalService>();

        // Act
        var result = await sut.GetAwaitingApprovalAsync(CancellationToken.None);

        // Assert
        result.Episodes.Should().HaveCount(held.Length);
        VerifyLogged(LogLevel.Warning, Times.Once, held.Length.ToString(), "awaiting approval");
    }

    [Fact(DisplayName =
        "When no more episodes are awaiting approval than the expected maximum, then the count is logged at Information and not Warning, because a normal held set is not an alert (#1491).")]
    public async Task awaiting_approval_count_within_expected_maximum_is_logged_at_information()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        _episodes.Seed(_fixture.CreateStoredEpisode(podcast, e => e.ApprovalStatus = ApprovalStatus.AwaitingApproval));
        var sut = _mocker.CreateInstance<EpisodeAwaitingApprovalService>();

        // Act
        await sut.GetAwaitingApprovalAsync(CancellationToken.None);

        // Assert
        VerifyLogged(LogLevel.Information, Times.Once, "awaiting approval");
        VerifyLogged(LogLevel.Warning, Times.Never, "awaiting approval");
    }

    private void VerifyLogged(LogLevel level, Func<Times> times, params string[] fragments) =>
        _mocker.GetMock<ILogger<EpisodeAwaitingApprovalService>>().Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => fragments.All(fragment => state!.ToString()!.Contains(fragment))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
}
