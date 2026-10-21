using RedditPodcastPoster.Models.TvShows;

namespace Api.Dtos.Extensions;

public static class TvShowEpisodeExtension
{
    public static TvShowEpisodeDto ToDto(this TvShowEpisode episode) =>
        new TvShowEpisodeDto
        {
            TvShowId = episode.TvShowId,
            TvShowName = episode.TvShowName,
            Imdb = episode.Imdb,
            ImdbId = episode.ImdbId,
            Tvdb = episode.Tvdb,
            TvdbId = episode.TvdbId,
            TmdbId = episode.TmdbId
        }.WithPlayableFields(episode);
}
