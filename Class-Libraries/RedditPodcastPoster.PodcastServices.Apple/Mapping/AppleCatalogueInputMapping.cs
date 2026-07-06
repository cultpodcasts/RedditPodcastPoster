using RedditPodcastPoster.Episodes.Adapters.Inputs;

namespace RedditPodcastPoster.PodcastServices.Apple.Mapping;

public static class AppleCatalogueInputMapping
{
    public static AppleCatalogueInput ToCatalogueInput(this AppleEpisode episode) =>
        new(
            episode.Id,
            episode.Title.Trim(),
            episode.Description.Trim(),
            episode.Duration,
            episode.Release,
            episode.Url.CleanAppleUrl(),
            episode.Image);
}
