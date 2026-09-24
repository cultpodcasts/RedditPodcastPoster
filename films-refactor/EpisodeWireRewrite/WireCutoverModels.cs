using System.Text.Json;

namespace EpisodeWireRewrite;

/// <summary>Cutover wire names only — migrate and rollback touch nothing else.</summary>
public static class WireCutoverFields
{
    public const string PodcastSearchTerms = "podcastSearchTerms";
    public const string PublisherSearchTerms = "publisherSearchTerms";
    public const string PodcastLanguage = "podcastLanguage";
    public const string PublisherLanguage = "publisherLanguage";
    public const string PodcastMetadataVersion = "podcastMetadataVersion";
    public const string ParentMetadataVersion = "parentMetadataVersion";
    public const string PodcastRemoved = "podcastRemoved";
    public const string ParentRemoved = "parentRemoved";
    public const string ReleaseSort = "releaseSort";
    public const string Release = "release";
    public const string Id = "id";
    public const string PodcastId = "podcastId";
    public const string Type = "type";

    public static readonly string[] SnapshotPaths =
    [
        PodcastSearchTerms,
        PublisherSearchTerms,
        PodcastLanguage,
        PublisherLanguage,
        PodcastMetadataVersion,
        ParentMetadataVersion,
        PodcastRemoved,
        ParentRemoved,
        ReleaseSort
    ];
}

/// <summary>One property before migrate (or target for rollback).</summary>
public sealed class WireFieldState
{
    public required string Name { get; init; }
    public bool Exists { get; init; }
    /// <summary>Raw JSON token when <see cref="Exists"/>; otherwise null.</summary>
    public string? JsonValue { get; init; }
}

/// <summary>Explicit from→to for one cutover field (operator repair log).</summary>
public sealed class WireFieldChange
{
    public required string Name { get; init; }
    public bool FromExists { get; init; }
    public string? FromJson { get; init; }
    public bool ToExists { get; init; }
    public string? ToJson { get; init; }

    public string ToDisplayLine()
    {
        static string Fmt(bool exists, string? json) =>
            exists ? (json ?? "<null>") : "<absent>";
        return $"{Name}: {Fmt(FromExists, FromJson)} -> {Fmt(ToExists, ToJson)}";
    }
}

/// <summary>
/// One episode change: forward desired state + before snapshot for rollback.
/// Rollback restores <see cref="Before"/> exactly for cutover paths.
/// </summary>
public sealed class WireCutoverChange
{
    public required Guid EpisodeId { get; init; }
    public required Guid PodcastId { get; init; }
    public required IReadOnlyList<WireFieldState> Before { get; init; }
    public required IReadOnlyList<WireFieldState> After { get; init; }
    public required IReadOnlyList<WireFieldChange> FieldChanges { get; init; }
    public required string Reason { get; init; }
}

public readonly record struct WireEpisodeRef(Guid PodcastId, Guid EpisodeId);
