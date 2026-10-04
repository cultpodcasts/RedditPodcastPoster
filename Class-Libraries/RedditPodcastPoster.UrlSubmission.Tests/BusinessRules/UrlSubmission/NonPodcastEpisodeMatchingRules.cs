using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class NonPodcastEpisodeMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IEpisodeHelper Sut => _mocker.CreateInstance<EpisodeHelper>();

    [Fact(DisplayName =
        "When a BBC Sounds URL is submitted against a podcast whose Spotify episode has the same title, " +
        "the stored episode matches so Sounds can be attached instead of creating a duplicate.")]
    public void bbc_sounds_submit_matches_existing_spotify_episode_by_title()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: title);
        var categorisedItem = OtherSoundsSubmit(podcast, [episode], title);

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a BBC Sounds URL is submitted against a podcast episode with a different title and no Sounds URL, " +
        "the stored episode does not match.")]
    public void bbc_sounds_submit_does_not_match_unrelated_title()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: _fixture.CreateTitle());
        var categorisedItem = OtherSoundsSubmit(podcast, [episode], _fixture.CreateTitle());

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the stored episode already has the submitted BBC Sounds URL, it matches even if titles differ.")]
    public void bbc_sounds_submit_matches_existing_sounds_url()
    {
        // Arrange
        var soundsUrl = SoundsPlayUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: _fixture.CreateTitle());
        EpisodeServicePresence.Upsert(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds), soundsUrl, null);
        var categorisedItem = OtherSoundsSubmit(podcast, [episode], _fixture.CreateTitle(), soundsUrl);

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeTrue();
    }

    private CategorisedItem OtherSoundsSubmit(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        string title,
        Uri? soundsUrl = null)
    {
        return new CategorisedItem(
            podcast,
            episodes,
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                StreamingService.BbcSounds,
                podcast,
                null,
                soundsUrl ?? SoundsPlayUrl(),
                title),
            Service.Other);
    }

    private Uri SoundsPlayUrl()
    {
        var playId = _fixture.CreateGuid().ToString("N");
        return new Uri($"https://www.bbc.co.uk/sounds/play/{playId}");
    }
}
