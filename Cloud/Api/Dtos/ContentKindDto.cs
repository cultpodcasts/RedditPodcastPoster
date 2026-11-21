namespace Api.Dtos;

/// <summary>
/// Playable kind on episode read models (<c>contentKind</c>). Names and numeric values mirror the domain
/// <c>RedditPodcastPoster.Models.ContentKinds.ContentKind</c>, and properties write it with
/// <c>JsonStringEnumConverter</c>, so the JSON text is the same as the string it replaced (#1508).
/// </summary>
public enum ContentKindDto
{
    Episode,
    TvShowEpisode,
    Film,
    NewsReport
}
