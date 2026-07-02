using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

public static class EpisodeFixtures
{
    private static readonly Guid DefaultPodcastId = Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2");

    public static Episode SubmittedViaSpotifyUrlOnly(
        Uri spotifyUrl,
        string title = "Reddit post title",
        DateTime? release = null,
        Guid? podcastId = null,
        Guid? episodeId = null) =>
        new()
        {
            Id = episodeId ?? Guid.NewGuid(),
            PodcastId = podcastId ?? DefaultPodcastId,
            Title = title,
            Release = release ?? DateTime.UtcNow.Date,
            SpotifyId = string.Empty,
            Urls = new ServiceUrls { Spotify = spotifyUrl }
        };

    public static Episode FromSpotifyCatalogue(
        string spotifyId,
        string title,
        Uri spotifyUrl,
        DateTime release,
        TimeSpan length,
        string description = "Catalogue description") =>
        Episode.FromSpotify(
            spotifyId,
            title,
            description,
            length,
            false,
            release,
            spotifyUrl,
            null);

    public static Episode FromYouTubeVideo(
        string youTubeId,
        string title,
        DateTime release,
        TimeSpan length,
        string description = "YouTube description") =>
        Episode.FromYouTube(
            youTubeId,
            title,
            description,
            length,
            false,
            release,
            new Uri($"https://www.youtube.com/watch?v={youTubeId}"),
            null);
}
