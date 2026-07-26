using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Heroes;

public class HeroAutoPromoteSelectorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Always-promote podcast with a newly indexed in-week episode: selector returns that episode id, because indexing should auto-append it to heroes.")]
    public void selects_in_week_episode_when_flagged()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var episodeId = _fixture.CreateGuid();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Id = episodeId;
            e.Release = DomainTestFixture.UtcDaysAgo(2);
            e.Ignored = false;
            e.Removed = false;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [episode],
            DomainTestFixture.UtcToday);

        // Assert
        ids.Should().Equal(episodeId);
    }

    [Fact(DisplayName =
        "Always-promote off: selector returns no ids for newly indexed episodes, because only flagged podcasts auto-promote.")]
    public void skips_when_flag_off()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = false;
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(1);
            e.Ignored = false;
            e.Removed = false;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [episode],
            DomainTestFixture.UtcToday);

        // Assert
        ids.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Always-promote podcast with an episode older than one week: selector returns no ids, because heroes only cover the current week window.")]
    public void skips_episodes_older_than_week()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(8);
            e.Ignored = false;
            e.Removed = false;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [episode],
            DomainTestFixture.UtcToday);

        // Assert
        ids.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Always-promote podcast with ignored or removed newly indexed episodes: selector returns no ids, because those episodes are not hero-eligible.")]
    public void skips_ignored_or_removed()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = true;
        var ignored = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(1);
            e.Ignored = true;
            e.Removed = false;
        });
        var removed = _fixture.CreateEpisode(e =>
        {
            e.Release = DomainTestFixture.UtcDaysAgo(1);
            e.Ignored = false;
            e.Removed = true;
        });

        // Act
        var ids = HeroAutoPromoteSelector.SelectEpisodeIds(
            podcast,
            [ignored, removed],
            DomainTestFixture.UtcToday);

        // Assert
        ids.Should().BeEmpty();
    }
}
