using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class SubmitEpisodeDetailsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When an episode has only Spotify/Apple/YouTube links, FromEpisode ExtraServiceKeys is null, because those platforms are named flags not streaming keys.")]
    public void from_episode_extra_service_keys_null_when_only_podcast_platforms()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var details = SubmitEpisodeDetails.FromEpisode(episode);

        // Assert
        details.Spotify.Should().Be(EpisodeServicePresence.HasUrl(episode, ServiceKeys.Spotify));
        details.Apple.Should().Be(EpisodeServicePresence.HasUrl(episode, ServiceKeys.Apple));
        details.YouTube.Should().Be(EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube));
        details.ExtraServiceKeys.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an episode has a Vimeo URL, FromEpisode ExtraServiceKeys contains vimeo, because streaming presence is the extra-keys bag not a named Vimeo flag.")]
    public void from_episode_extra_service_keys_contains_vimeo()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var vimeoUrl = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        EpisodeServicePresence.Upsert(episode, StreamingServiceKeys.Vimeo, vimeoUrl, null);

        // Act
        var details = SubmitEpisodeDetails.FromEpisode(episode);

        // Assert
        details.ExtraServiceKeys.Should().Contain(StreamingServiceKeys.Vimeo);
        details.ExtraServiceKeys.Should().NotContain(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "When an episode has a BBC iPlayer URL, FromEpisode ExtraServiceKeys contains bbcIplayer, because BBC is not a combined named flag.")]
    public void from_episode_extra_service_keys_contains_bbc_iplayer()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var iplayerUrl = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        EpisodeServicePresence.Upsert(episode, StreamingServiceKeys.BbcIplayer, iplayerUrl, null);

        // Act
        var details = SubmitEpisodeDetails.FromEpisode(episode);

        // Assert
        details.ExtraServiceKeys.Should().Equal(StreamingServiceKeys.BbcIplayer);
    }

    [Fact(DisplayName =
        "When an episode has Internet Archive, Netflix, and Prime URLs, FromEpisode ExtraServiceKeys lists each catalog key, because the named bools were retired.")]
    public void from_episode_extra_service_keys_lists_archive_netflix_and_prime()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            StreamingServiceKeys.InternetArchive,
            new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}"),
            null);
        EpisodeServicePresence.Upsert(
            episode,
            StreamingServiceKeys.Netflix,
            new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}"),
            null);
        EpisodeServicePresence.Upsert(
            episode,
            StreamingServiceKeys.AmazonPrime,
            new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}"),
            null);

        // Act
        var details = SubmitEpisodeDetails.FromEpisode(episode);

        // Assert
        details.ExtraServiceKeys.Should().Contain(StreamingServiceKeys.InternetArchive);
        details.ExtraServiceKeys.Should().Contain(StreamingServiceKeys.Netflix);
        details.ExtraServiceKeys.Should().Contain(StreamingServiceKeys.AmazonPrime);
    }
}
