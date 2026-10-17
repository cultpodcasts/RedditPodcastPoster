namespace CultPodcasts.DatabasePublisher.Tests;

/// <summary>
/// The publisher renders a Spectre.Console progress display, and only one interactive display may run at a
/// time, so every test class that calls <c>Run</c> joins this collection and runs sequentially.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PublisherCollection
{
    public const string Name = "PublicDatabasePublisher";
}
