using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

public static class PodcastFixtures
{
    public static Podcast Standard(Guid? id = null, string name = "Test Podcast") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name
        };

    public static Podcast SpotifyPrimary(string spotifyShowId, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Spotify-primary podcast",
            SpotifyId = spotifyShowId,
            ReleaseAuthority = Service.Spotify
        };

    public static Podcast YouTubeFirst(
        string channelId,
        long youTubePublicationOffsetTicks,
        string? spotifyShowId = null,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200"),
            Name = "YouTube-first podcast",
            ReleaseAuthority = Service.YouTube,
            YouTubeChannelId = channelId,
            YouTubePublicationOffset = youTubePublicationOffsetTicks,
            SpotifyId = spotifyShowId ?? string.Empty
        };

    public static Podcast CultsToConsciousness() =>
        YouTubeFirst(
            channelId: "c2c-channel",
            youTubePublicationOffsetTicks: TimeSpan.FromDays(-33).Add(TimeSpan.FromHours(-12)).Ticks,
            spotifyShowId: "6oTbi9wKZ2czCvSwBKxxoH",
            id: Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200"));
}
