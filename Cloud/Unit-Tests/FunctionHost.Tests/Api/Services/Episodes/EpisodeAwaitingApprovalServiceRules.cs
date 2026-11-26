using Api.Models;
using Api.Services.Episodes;
using FluentAssertions;
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
        "When an episode awaiting approval has no stored podcast, then the list still includes it under the name stored on the episode, because the curator must still open it to approve or reject (#1491).")]
    public async Task missing_podcast_still_lists_the_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var nameOnEpisode = _fixture.CreateTitle();
        var held = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ApprovalStatus = ApprovalStatus.AwaitingApproval;
            e.PodcastName = nameOnEpisode;
        });
        _episodes.Seed(held);
        var sut = _mocker.CreateInstance<EpisodeAwaitingApprovalService>();

        // Act
        var result = await sut.GetAwaitingApprovalAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeAwaitingApprovalStatus.Ok);
        var pair = result.Episodes.Should().ContainSingle().Subject;
        pair.Episode.Id.Should().Be(held.Id);
        pair.Podcast.Id.Should().Be(podcast.Id);
        pair.Podcast.Name.Should().Be(nameOnEpisode);
    }
}
