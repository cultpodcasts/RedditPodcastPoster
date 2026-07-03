namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Incident and regression constants for episode specimens.
/// Use <see cref="DomainTestFixture"/> to create episode instances.
/// </summary>
public static class EpisodeFixtures
{
    public static readonly Guid DefaultPodcastId = Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2");

    public static readonly Guid C2CAbuserEpisodeId = Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362");

    public static readonly Guid C2CNegativeDelayEpisodeId = Guid.Parse("53ba0c64-58a7-4292-b7fe-ba135d4d3160");

    public static readonly Guid C2COtoOwnerEpisodeId = Guid.Parse("1c804814-12ac-40c8-a223-88ab7c703d38");

    public static readonly Guid PostmormonExistingEpisodeId = Guid.Parse("086b02d5-9ec7-432e-8e57-9279d32374da");
}
