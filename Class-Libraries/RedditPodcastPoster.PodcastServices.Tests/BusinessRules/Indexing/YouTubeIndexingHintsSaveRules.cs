using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>A podcast whose YouTube indexing hints changed is saved, even on a pass that does not count as indexed.</summary>
public class YouTubeIndexingHintsSaveRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A pass whose only change is refreshed YouTube indexing hints still saves the podcast, even when the pass was " +
        "bypassed and LastIndexed is not set, because losing the hints means another full playlist walk.")]
    public async Task hints_only_change_is_saved()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);
        var hints = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            LastFullWalk = DomainTestFixture.UtcDaysAgo(0),
            PlaylistItemCount = 400
        };
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((indexed, _, context) =>
            {
                indexed.YouTubeIndexingHints = hints;
                context.SkipSpotifyUrlResolving = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(podcast, enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().ContainSingle();
        harness.PodcastRepository.GetStored(podcast.Id).YouTubeIndexingHints.Should().Be(hints);
    }
}
