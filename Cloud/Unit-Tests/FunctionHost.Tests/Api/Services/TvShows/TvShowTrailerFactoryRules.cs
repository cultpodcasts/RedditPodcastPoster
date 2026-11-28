using FluentAssertions;
using Api.Services.Films;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

/// <summary>RPP #1412: a TV show trailer has the film trailer shape, built by the one film rule.</summary>
public class TvShowTrailerFactoryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A TV show trailer is the film trailer for the same episode: same id, title, playback url, duration, platform ids and service urls, because both attach flows share one rule.")]
    public void tv_show_trailer_matches_film_trailer_for_the_same_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(podcast);
        var title = _fixture.CreateTitle();

        // Act
        var trailer = TvShowTrailerFactory.From(episode, title);

        // Assert
        var film = FilmTrailerFactory.From(episode, title)!;
        trailer.Should().NotBeNull();
        trailer!.Id.Should().Be(episode.Id).And.Be(film.Id);
        trailer.Title.Should().Be(title).And.Be(film.Title);
        trailer.Url.Should().Be(film.Url);
        trailer.Length.Should().Be(film.Length);
        trailer.Release.Should().Be(film.Release);
        trailer.Ids.Should().BeEquivalentTo(film.Ids);
        trailer.Services.Should().BeEquivalentTo(film.Services);
        trailer.Services!.Should().ContainKey(ServiceKeys.YouTube).And.ContainKey(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "An episode with no link to play from has no TV show trailer, so the attach is refused instead of storing a trailer nobody can play.")]
    public void episode_without_a_link_has_no_trailer()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Services = null;

        // Act
        var trailer = TvShowTrailerFactory.From(episode, null);

        // Assert
        trailer.Should().BeNull();
    }
}

