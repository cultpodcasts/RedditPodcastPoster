using RedditPodcastPoster.Models.ContentKinds;

namespace Api.Dtos.Mapping;

/// <summary>Maps the domain playable kind to <see cref="ContentKindDto"/>; throws on an unmapped value so drift fails loudly.</summary>
public static class ContentKindDtoMappers
{
    public static ContentKindDto ToDto(this ContentKind kind) =>
        kind switch
        {
            ContentKind.Episode => ContentKindDto.Episode,
            ContentKind.TvShowEpisode => ContentKindDto.TvShowEpisode,
            ContentKind.Film => ContentKindDto.Film,
            ContentKind.NewsReport => ContentKindDto.NewsReport,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped ContentKind value.")
        };

    /// <summary>
    /// Maps a stored kind name (written with <c>nameof(ContentKind.X)</c>). Null stays null; any other text
    /// throws, because sending it under a different JSON name would silently change the wire.
    /// </summary>
    public static ContentKindDto? ToContentKindDto(this string? kind) =>
        kind switch
        {
            null => null,
            nameof(ContentKind.Episode) => ContentKind.Episode.ToDto(),
            nameof(ContentKind.TvShowEpisode) => ContentKind.TvShowEpisode.ToDto(),
            nameof(ContentKind.Film) => ContentKind.Film.ToDto(),
            nameof(ContentKind.NewsReport) => ContentKind.NewsReport.ToDto(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown content kind name.")
        };
}
