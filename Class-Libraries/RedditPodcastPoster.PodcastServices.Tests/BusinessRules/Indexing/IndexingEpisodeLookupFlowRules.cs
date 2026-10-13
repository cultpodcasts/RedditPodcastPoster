using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using Moq;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The indexer marks its lookups as the Index flow and writes show sizes observed during the pass onto the podcast
/// (<c>spotifyEpisodeCount</c> / <c>appleEpisodeCount</c>), each from its own platform only.
/// </summary>
public class IndexingEpisodeLookupFlowRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "PodcastUpdater.Update sets IndexingContext.LookupFlow to Index on the shared context before enrichment, " +
        "because the planner reads newest pages without searching only in the Index flow.")]
    public async Task Update_sets_index_flow()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness(new SaveCallRecorder());
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        harness.PodcastRepository.Seed(podcast);
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext();
        EpisodeLookupFlow? seen = null;
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback((Podcast _, IEnumerable<Episode> _, IndexingContext c) => seen = c.LookupFlow)
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(podcast, enrichOnly: false, context);

        // Assert
        seen.Should().Be(EpisodeLookupFlow.Index);
        context.LookupFlow.Should().Be(EpisodeLookupFlow.Index);
    }

    [Fact(DisplayName =
        "When the pass observed Spotify and Apple show sizes, Update writes each onto its own podcast field and saves the podcast; " +
        "a count for another show id is ignored.")]
    public async Task Observed_counts_are_written_per_platform()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness(new SaveCallRecorder());
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = 1603773245;
        harness.PodcastRepository.Seed(podcast);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);
        harness.ShowEpisodeCounts.Record(CataloguePlatforms.Spotify, podcast.SpotifyId, 412);
        harness.ShowEpisodeCounts.Record(CataloguePlatforms.Apple, "1603773245", 255);
        harness.ShowEpisodeCounts.Record(CataloguePlatforms.Spotify, "another-show", 9);

        // Act
        await harness.Updater.Update(podcast, enrichOnly: false, PodcastUpdaterTestHarness.DefaultIndexingContext());

        // Assert
        podcast.SpotifyEpisodeCount.Should().Be(412);
        podcast.AppleEpisodeCount.Should().Be(255);
        harness.PodcastRepository.SavedPodcasts.Should().Contain(x => x.Id == podcast.Id && x.SpotifyEpisodeCount == 412 && x.AppleEpisodeCount == 255);
    }

    [Fact(DisplayName = "When nothing was observed, Update leaves both counts null (historic records stay unknown).")]
    public async Task No_observation_leaves_counts_null()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness(new SaveCallRecorder());
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        harness.PodcastRepository.Seed(podcast);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(podcast, enrichOnly: false, PodcastUpdaterTestHarness.DefaultIndexingContext());

        // Assert
        podcast.SpotifyEpisodeCount.Should().BeNull();
        podcast.AppleEpisodeCount.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the observed Spotify count equals the stored one, Update leaves the stored count as it is and the Apple count null, " +
        "because counts change only from an observation for that platform's own show id.")]
    public async Task Unchanged_count_is_kept()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness(new SaveCallRecorder());
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.SpotifyEpisodeCount = 412;
        harness.PodcastRepository.Seed(podcast);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);
        harness.ShowEpisodeCounts.Record(CataloguePlatforms.Spotify, podcast.SpotifyId, 412);

        // Act
        await harness.Updater.Update(podcast, enrichOnly: false, PodcastUpdaterTestHarness.DefaultIndexingContext());

        // Assert
        podcast.SpotifyEpisodeCount.Should().Be(412);
        podcast.AppleEpisodeCount.Should().BeNull();
    }
}
