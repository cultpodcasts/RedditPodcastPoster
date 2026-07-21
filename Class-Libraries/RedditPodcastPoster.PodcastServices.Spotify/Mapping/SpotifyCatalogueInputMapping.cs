using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.PodcastServices.Spotify.Mapping;

public static class SpotifyCatalogueInputMapping
{
    public static SpotifyCatalogueInput ToCatalogueInput(this SimpleEpisode episode, IHtmlSanitiser htmlSanitiser) =>
        new(
            episode.Id,
            episode.Name.Trim(),
            htmlSanitiser.Sanitise(episode.HtmlDescription ?? string.Empty),
            episode.GetDuration(),
            episode.GetReleaseDate(),
            new Uri(episode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
            episode.GetBestImageUrl());

    public static SpotifyCatalogueInput ToCatalogueInput(this FullEpisode episode, IHtmlSanitiser htmlSanitiser) =>
        new(
            episode.Id,
            episode.Name.Trim(),
            htmlSanitiser.Sanitise(episode.HtmlDescription ?? string.Empty),
            episode.GetDuration(),
            episode.GetReleaseDate(),
            episode.GetUrl(),
            episode.GetBestImageUrl());
}
