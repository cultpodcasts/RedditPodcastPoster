using FluentAssertions;
using RedditPodcastPoster.PlayRts.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret

namespace RedditPodcastPoster.PlayRts.Tests.BusinessRules;

public class PlayRtsUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Play RTS TV episode URL with a show slug and video slug is a submit URL, so submit can ingest a catalogue episode.")]
    public void tv_episode_is_submit_url()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}/video/{episode}");

        // Act
        var matches = PlayRtsUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Play RTS TV episode URL with a urn query is a submit URL, because Play RTS identity lives in the urn and must be kept.")]
    public void tv_episode_with_urn_query_is_submit_url()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateYouTubeId();
        var urn = _fixture.CreateGuid();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}/video/{episode}?urn=urn:rts:video:{urn}");

        // Act
        var matches = PlayRtsUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Play RTS TV series hub URL with a show slug is a submit URL, because the catalogue page still identifies a title.")]
    public void tv_series_hub_is_submit_url()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}");

        // Act
        var matches = PlayRtsUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Play RTS radio audio URL is a submit URL, because radio uses /play/radio/{slug}/audio/{episode}.")]
    public void radio_audio_is_submit_url()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/radio/{show}/audio/{episode}");

        // Act
        var matches = PlayRtsUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "The Play RTS marketing root is not a submit URL, because it is not a catalogue title.")]
    public void marketing_root_is_not_submit_url()
    {
        // Arrange
        var url = new Uri("https://www.rts.ch/");

        // Act
        var matches = PlayRtsUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Play RTS news article path is not a submit URL, because only /play/tv and /play/radio catalogue paths ingest.")]
    public void news_path_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.rts.ch/info/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = PlayRtsUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Play RTS series hub path is not an episode path, because /play/tv/{slug} is the catalogue show rather than a video or audio asset.")]
    public void series_hub_is_not_episode_path()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}");

        // Act
        var isEpisode = PlayRtsUrlMatcher.IsEpisodePath(url);

        // Assert
        isEpisode.Should().BeFalse();
        PlayRtsUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Play RTS TV video URL is an episode path, because /play/tv/{slug}/video/{episode} is the catalogue asset.")]
    public void tv_video_is_episode_path()
    {
        // Arrange
        var show = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/{show}/video/{episode}");

        // Act
        var isEpisode = PlayRtsUrlMatcher.IsEpisodePath(url);

        // Assert
        isEpisode.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Play RTS series slug of video is not an episode path, because /play/tv/video is a show slug rather than a video|audio segment at index 3.")]
    public void series_slug_video_is_not_episode_path()
    {
        // Arrange
        var url = new Uri("https://www.rts.ch/play/tv/video");

        // Act
        var isEpisode = PlayRtsUrlMatcher.IsEpisodePath(url);

        // Assert
        isEpisode.Should().BeFalse();
        PlayRtsUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Play RTS episode under a series slug of video is an episode path, because video at path index 3 is the medium and index 4 is the episode slug.")]
    public void series_slug_video_with_episode_is_episode_path()
    {
        // Arrange
        var episode = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.rts.ch/play/tv/video/video/{episode}");

        // Act
        var isEpisode = PlayRtsUrlMatcher.IsEpisodePath(url);

        // Assert
        isEpisode.Should().BeTrue();
        PlayRtsUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
    }
}
