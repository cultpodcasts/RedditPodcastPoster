using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Factories;

public class FindSpotifyEpisodeRequestFactoryTests
{
    [Fact]
    public void Create_WhenEpisodeDiscoveredViaYouTubeOnSpotifyPrimaryPodcast_AdjustsReleaseAndUsesDurationMatching()
    {
        var delay = TimeSpan.FromHours(9);
        var podcast = new Podcast
        {
            SpotifyId = "spotify-show",
            Name = "The Shadow Sessions Podcast",
            YouTubePublicationOffset = delay.Ticks
        };
        var youTubePublish = new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Utc);
        var episode = new Episode
        {
            Title = "My Family Was America's Most Dangerous Cult",
            Release = youTubePublish,
            Length = TimeSpan.FromMinutes(62),
            YouTubeId = "video-id",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=video-id") }
        };

        var request = FindSpotifyEpisodeRequestFactory.Create(podcast, episode);

        request.Released.Should().Be(youTubePublish - delay);
        request.EnrichingYouTubeDiscoveredEpisode.Should().BeTrue();
        request.Length.Should().Be(episode.Length);
    }
}
