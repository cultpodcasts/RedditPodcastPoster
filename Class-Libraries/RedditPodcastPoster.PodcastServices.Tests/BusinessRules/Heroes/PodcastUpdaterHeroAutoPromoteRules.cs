using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Heroes;

public class PodcastUpdaterHeroAutoPromoteRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Indexing a flagged podcast promotes newly added recent episodes as heroes and skips ones outside the week window.")]
    public async Task flagged_podcast_promotes_only_recent_added_episodes()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = true;
        harness.PodcastRepository.Seed(podcast);

        var recent = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1))
            .WithDuration(PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration + TimeSpan.FromMinutes(5))
            .WithDescription(_fixture.Create<string>()));
        var old = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(30))
            .WithDescription(_fixture.Create<string>()));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([recent, old]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.EpisodeRepository.SavedEpisodes.Select(x => x.Id)
            .Should().BeEquivalentTo([recent.Id, old.Id]);
        harness.HeroEpisodePromoter.Verify(
            x => x.PromoteAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == recent.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Indexing a flagged podcast does not call the hero promoter when every added episode is skipped.")]
    public async Task flagged_podcast_with_only_old_episodes_does_not_promote()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = true;
        harness.PodcastRepository.Seed(podcast);

        var old = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(30))
            .WithDescription(_fixture.Create<string>()));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([old]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.EpisodeRepository.SavedEpisodes.Should().ContainSingle();
        harness.HeroEpisodePromoter.Verify(
            x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
