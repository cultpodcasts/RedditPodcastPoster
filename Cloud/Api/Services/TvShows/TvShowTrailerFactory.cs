using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.TvShows;
using Api.Services.Films;

namespace Api.Services.TvShows;

/// <summary>
/// Builds a <see cref="TvShowTrailer"/> from the podcast episode being attached. A TV show trailer has the same JSON
/// shape as a film trailer (RPP #1412), so the playback-url choice, the copied platform ids and the copied service
/// urls are exactly <see cref="FilmTrailerFactory"/>'s: there is one rule for both.
/// </summary>
public static class TvShowTrailerFactory
{
    /// <summary>The trailer for <paramref name="episode"/>, or null when the episode has no link to play it from.</summary>
    public static TvShowTrailer? From(Episode episode, string? title)
    {
        var trailer = FilmTrailerFactory.From(episode, title);
        return trailer is null
            ? null
            : new TvShowTrailer
            {
                Id = trailer.Id,
                Title = trailer.Title,
                Url = trailer.Url,
                Image = trailer.Image,
                Release = trailer.Release,
                Length = trailer.Length,
                Ids = trailer.Ids,
                Services = trailer.Services
            };
    }
}

