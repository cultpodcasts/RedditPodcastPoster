using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>The kinds of item the public database holds, one flat folder, told apart by file-key prefix.</summary>
public enum PublicKind
{
    Podcast,
    TvShow,
    NewsOrganisation,
    Film
}

public static class PublicKindExtensions
{
    private static readonly string[] ReservedPrefixes =
    [
        FileKeyFactory.FilmPrefix,
        FileKeyFactory.TvShowPrefix,
        FileKeyFactory.NewsOrganisationPrefix
    ];

    /// <summary>The prefix a kind's file keys must start with; podcasts have none and must not use a reserved one.</summary>
    public static string? RequiredPrefix(this PublicKind kind) =>
        kind switch
        {
            PublicKind.Podcast => null,
            PublicKind.TvShow => FileKeyFactory.TvShowPrefix,
            PublicKind.NewsOrganisation => FileKeyFactory.NewsOrganisationPrefix,
            PublicKind.Film => FileKeyFactory.FilmPrefix,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown public kind.")
        };

    /// <summary>The kind a file key belongs to, judged by its prefix; unprefixed keys are podcasts.</summary>
    public static PublicKind FromFileKey(string fileKey) =>
        fileKey.StartsWith(FileKeyFactory.TvShowPrefix, StringComparison.OrdinalIgnoreCase) ? PublicKind.TvShow
        : fileKey.StartsWith(FileKeyFactory.NewsOrganisationPrefix, StringComparison.OrdinalIgnoreCase) ? PublicKind.NewsOrganisation
        : fileKey.StartsWith(FileKeyFactory.FilmPrefix, StringComparison.OrdinalIgnoreCase) ? PublicKind.Film
        : PublicKind.Podcast;

    public static bool UsesReservedPrefix(string fileKey) =>
        ReservedPrefixes.Any(prefix => fileKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
