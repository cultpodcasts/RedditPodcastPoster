namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Incident and regression constants for podcast specimens.
/// Use <see cref="DomainTestFixture"/> to create podcast instances.
/// </summary>
public static class PodcastFixtures
{
    public static readonly Guid CultsToConsciousnessPodcastId = Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200");

    public const string CultsToConsciousnessChannelId = "c2c-channel";

    public const string CultsToConsciousnessSpotifyShowId = "6oTbi9wKZ2czCvSwBKxxoH";

    public static readonly long CultsToConsciousnessYouTubePublicationOffsetTicks =
        TimeSpan.FromDays(-33).Add(TimeSpan.FromHours(-12)).Ticks;
}
