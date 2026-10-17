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

    public static bool UsesReservedPrefix(string fileKey) =>
        ReservedPrefixes.Any(prefix => fileKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
