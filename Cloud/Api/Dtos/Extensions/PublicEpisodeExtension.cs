namespace Api.Dtos.Extensions;

public static class PublicEpisodeExtension
{
    public static PublicEpisodeDto ToDto(
        this RedditPodcastPoster.Models.Episodes.Episode episode,
        RedditPodcastPoster.Models.Podcasts.Podcast podcast)
    {
        return new PublicEpisodeDto
        {
            PodcastName = podcast.Name,
            Id = episode.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Urls = episode.Urls,
            Subjects = episode.Subjects,
            Image = episode.Images?.YouTube ??
                    episode.Images?.Spotify ??
                    episode.Images?.Apple ??
                    episode.Images?.Other
        };
    }
}
