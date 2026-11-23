using Api.Dtos.Mapping;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Api.Dtos.Extensions;

public static class PublicEpisodeExtension
{
    public static PublicEpisodeDto ToDto(
        this RedditPodcastPoster.Models.Episodes.Episode episode,
        RedditPodcastPoster.Models.Podcasts.Podcast podcast)
    {
        EpisodeServicePresence.NormalizeCatalog(episode);
        return new PublicEpisodeDto
        {
            PodcastName = podcast.Name,
            Id = episode.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.ReleaseUtc,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Ids = episode.Ids.ToDto(),
            Services = episode.Services.ToDtos(),
            Subjects = episode.Subjects,
            Image = episode.IsImageRemoved()
                ? null
                : EpisodeServicePresence.CoalescedImage(episode, StreamingServiceCatalog.ImageCoalesceOrder),
            ImageRemoved = episode.IsImageRemoved() ? true : null
        };
    }
}
