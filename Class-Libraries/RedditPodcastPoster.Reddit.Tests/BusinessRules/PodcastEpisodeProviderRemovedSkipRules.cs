using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.SocialPosting.Episodes;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// Removed-podcast candidates must not continue into the ready filter after the skip log (#1209).
/// </summary>
public class PodcastEpisodeProviderRemovedSkipRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private bool _readyFilterInvoked;

    public PodcastEpisodeProviderRemovedSkipRules()
    {
        _mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = TimeSpan.FromMinutes(1),
            TweetDays = 2,
            RedditDays = 2,
            BlueSkyDays = 2,
            CategoriserDays = 2
        }));
        _mocker.GetMock<IPodcastEpisodeFilter>()
            .Setup(x => x.GetMostRecentUntweetedEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int>()))
            .ReturnsAsync((Podcast podcast, IEnumerable<Episode> episodes, bool _, bool __, int ___) =>
            {
                _readyFilterInvoked = true;
                return episodes.Select(e => new PodcastEpisode(podcast, e));
            });
    }

    [Fact(DisplayName =
        "When a candidate's podcast is removed, then the ready-episode filter is not invoked for that group, because the skip log means those episodes are dropped.")]
    public async Task removed_podcast_does_not_invoke_ready_filter()
    {
        // Arrange
        var removed = _fixture.CreatePodcast(p => p.Removed = true);
        var active = _fixture.CreatePodcast(p => p.Removed = false);
        var removedEpisode = _fixture.CreateStoredEpisode(removed, e => e.ReleaseUtc = DomainTestFixture.UtcToday);
        var activeEpisode = _fixture.CreateStoredEpisode(active, e => e.ReleaseUtc = DomainTestFixture.UtcToday);
        var candidates = new[]
        {
            new PodcastEpisode(removed, removedEpisode),
            new PodcastEpisode(active, activeEpisode)
        };
        var sut = _mocker.CreateInstance<PodcastEpisodeProvider>();

        // Act
        var ready = (await sut.GetUntweetedPodcastEpisodes(
            youTubeRefreshed: true,
            spotifyRefreshed: true,
            preloadedRecentCandidates: candidates)).ToArray();

        // Assert
        ready.Should().ContainSingle(x => x.Episode.Id == activeEpisode.Id);
        ready.Should().NotContain(x => x.Episode.Id == removedEpisode.Id);
        _mocker.GetMock<IPodcastEpisodeFilter>().Verify(
            x => x.GetMostRecentUntweetedEpisodes(
                It.Is<Podcast>(p => p.Id == removed.Id),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int>()),
            Times.Never);
        _mocker.GetMock<IPodcastEpisodeFilter>().Verify(
            x => x.GetMostRecentUntweetedEpisodes(
                It.Is<Podcast>(p => p.Id == active.Id),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int>()),
            Times.Once);
        _readyFilterInvoked.Should().BeTrue();
    }
}
