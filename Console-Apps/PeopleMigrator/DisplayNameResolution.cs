namespace PeopleMigrator;

public sealed class DisplayNameResolution
{
    public string? ChosenName { get; init; }

    public string? TwitterName { get; init; }

    public string? BlueskyName { get; init; }

    /// <summary>"twitter", "bluesky", or null when no usable API name.</summary>
    public string? ChosenSource { get; init; }
}
