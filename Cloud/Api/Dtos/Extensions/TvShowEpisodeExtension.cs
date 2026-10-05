using RedditPodcastPoster.Models.TvShows;

namespace Api.Dtos.Extensions;

public static class TvShowEpisodeExtension
{
    public static TvShowEpisodeDto ToDto(this TvShowEpisode episode)
    {
        return new TvShowEpisodeDto
        {
            Id = episode.Id,
            TvShowId = episode.TvShowId,
            Title = episode.Title,
            Imdb = episode.Imdb,
            ImdbId = episode.ImdbId,
            Tvdb = episode.Tvdb,
            TvdbId = episode.TvdbId,
            TmdbId = episode.TmdbId
        };
    }
}
