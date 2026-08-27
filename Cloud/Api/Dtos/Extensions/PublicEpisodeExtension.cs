using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret

namespace Api.Dtos.Extensions;

public static class PublicEpisodeExtension
{
    public static PublicEpisodeDto ToDto(
        this RedditPodcastPoster.Models.Episodes.Episode episode, // pragma: allowlist secret
        RedditPodcastPoster.Models.Podcasts.Podcast podcast)
    {
        EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret
        return new PublicEpisodeDto
        {
            PodcastName = podcast.Name,
            Id = episode.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Ids = episode.Ids,
            Services = episode.Services,
            Subjects = episode.Subjects,
            Image = EpisodeServicePresence.CoalescedImage(episode) // pragma: allowlist secret
        };
    }
}
