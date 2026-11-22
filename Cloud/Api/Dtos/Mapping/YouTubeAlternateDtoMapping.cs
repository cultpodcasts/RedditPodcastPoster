using Api.Dtos.Extensions;
using RedditPodcastPoster.Models.YouTube;

namespace Api.Dtos.Mapping;

public static class YouTubeAlternateDtoMapping
{
    public static YouTubeAlternateDto[]? ToDtos(IReadOnlyList<YouTubeAlternate>? alternates) =>
        alternates is { Count: > 0 }
            ? alternates.Select(ToDto).ToArray()
            : null;

    public static YouTubeAlternateDto ToDto(YouTubeAlternate alternate) =>
        new()
        {
            YouTubeId = alternate.YouTubeId,
            Url = alternate.Url,
            Image = alternate.Image,
            Title = alternate.Title,
            ChannelId = alternate.ChannelId,
            ChannelName = alternate.ChannelName,
            Release = alternate.Release,
            Duration = alternate.Length is { } length ? CatalogueDtoMapping.FormatDuration(length) : null,
            SourceEpisodeId = alternate.SourceEpisodeId
        };
}
