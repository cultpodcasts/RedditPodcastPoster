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

    [Fact(DisplayName =
        "A pass where a found episode fails to merge keeps the previous YouTube indexing hints, because moving the " +
        "watermark and count past that episode would stop the uploads check offering it again until the daily walk.")]
    public async Task failed_merge_keeps_previous_hints()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var previous = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            LastFullWalk = DomainTestFixture.UtcDaysAgo(1),
            PlaylistItemCount = 400
        };
        podcast.YouTubeIndexingHints = previous.Copy();
        harness.PodcastRepository.Seed(podcast);
        var sharedRelease = DomainTestFixture.UtcDaysAgo(32);
        var sharedLength = _fixture.CreateDuration();
        var sharedTitle = _fixture.CreateTitle();
        var (youTubeOnly, appleOnly) =
            _fixture.CreateAmbiguousMatchStoredEpisodes(podcast, sharedRelease, sharedLength, sharedTitle);
        harness.EpisodeRepository.Seed(youTubeOnly);
        harness.EpisodeRepository.Seed(appleOnly);
        var discovered = _fixture.CreateAmbiguousMatchSpotifyIncoming(sharedRelease, sharedLength, sharedTitle);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((indexed, _, _) =>
                indexed.YouTubeIndexingHints = previous with
                {
                    PlaylistItemCount = 401,
                    UploadsCheckedThrough = DomainTestFixture.UtcDaysAgo(0)
                })
            .ReturnsAsync([discovered]);

        // Act
        var result = await harness.Updater.Update(podcast, enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.MergeResult.FailedEpisodes.Should().ContainSingle();
        podcast.YouTubeIndexingHints.Should().Be(previous);
        harness.PodcastRepository.GetStored(podcast.Id).YouTubeIndexingHints.Should().Be(previous);
    }
}
