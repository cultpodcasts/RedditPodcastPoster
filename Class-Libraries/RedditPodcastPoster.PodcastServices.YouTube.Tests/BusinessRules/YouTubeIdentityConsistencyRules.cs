using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules;

public class YouTubeIdentityConsistencyRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a non-removed episode has a YouTube URL but no YouTube id, identity is inconsistent because catalog matching cannot trust one-sided YouTube data.")]
    public void url_without_id_on_live_episode_is_inconsistent()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, null);
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(youTubeId), null);
        });

        // Act
        var inconsistent = YouTubeUrlCategoriser.HasInconsistentYouTubeIdAndUrl(episode);

        // Assert
        inconsistent.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a YouTube id and URL encode the same video, identity is consistent so search matching can proceed.")]
    public void matching_id_and_url_are_consistent()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, youTubeId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(youTubeId), null);
        });

        // Act
        var inconsistent = YouTubeUrlCategoriser.HasInconsistentYouTubeIdAndUrl(episode);

        // Assert
        inconsistent.Should().BeFalse();
    }
}
