using CultPodcasts.DatabasePublisher.PublicModels;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;

namespace CultPodcasts.DatabasePublisher.Mapping;

/// <summary>
/// Maps TV shows, news organisations and films onto their public files. Conventions follow the podcast file:
/// blank text and empty lists are omitted, children are newest first, and the caller has already dropped
/// removed children. Ties on release are broken by id so the output does not depend on read order and
/// git sees no churn between runs; this applies to the new kinds only, podcast episodes keep the ordering
/// they have always had. <c>release</c> is a string (see <see cref="PublicRelease"/>); a child that the previous
/// file already had with the same release instant keeps that file's text (see <see cref="BaselineReleaseText"/>).
/// </summary>
public static class PublicCatalogueMapper
{
    public static PublicTvShow ToPublic(
        TvShow show,
        IEnumerable<TvShowEpisode> episodes,
        BaselineReleaseText? previous = null) =>
        new(show.Id)
        {
            FileKey = show.FileKey,
            Name = show.Name,
            ImdbId = NullIfBlank(show.ImdbId),
            TvdbId = show.TvdbId,
            TmdbId = show.TmdbId,
            Episodes = NewestFirst(episodes)
                .Select(e => new PublicTvShowEpisode
                {
                    Id = e.Id,
                    Title = e.Title,
                    Description = NullIfBlank(e.Description),
                    Release = (previous ?? BaselineReleaseText.None).Choose(e.Id, PublicRelease.Format(e.Release)),
                    Length = e.Length,
                    Explicit = e.Explicit,
                    Subjects = NullIfEmpty(e.Subjects),
                    Urls = ToUrls(e.Services),
                    ImdbId = NullIfBlank(e.ImdbId),
                    TvdbId = e.TvdbId,
                    TmdbId = e.TmdbId
                })
                .ToList()
        };

    public static PublicNewsOrganisation ToPublic(
        NewsOrganisation organisation,
        IEnumerable<NewsReport> reports,
        BaselineReleaseText? previous = null) =>
        new(organisation.Id)
        {
            FileKey = organisation.FileKey,
            Name = organisation.Name,
            Reports = NewestFirst(reports)
                .Select(r => new PublicNewsReport
                {
                    Id = r.Id,
                    Title = r.Title,
                    Description = NullIfBlank(r.Description),
                    Release = (previous ?? BaselineReleaseText.None).Choose(r.Id, PublicRelease.Format(r.Release)),
                    Length = r.Length,
                    Explicit = r.Explicit,
                    Subjects = NullIfEmpty(r.Subjects),
                    Urls = ToUrls(r.Services)
                })
                .ToList()
        };

    public static PublicFilm ToPublic(Film film) =>
        new(film.Id)
        {
            FileKey = film.FileKey,
            Name = film.Name,
            Description = NullIfBlank(film.Description),
            Release = PublicRelease.Format(film.Release),
            Length = film.Length,
            Explicit = film.Explicit,
            ImdbId = NullIfBlank(film.ImdbId),
            TmdbId = film.TmdbId,
            Subjects = NullIfEmpty(film.Subjects),
            Urls = ToUrls(film.Services)
        };

    private static IEnumerable<T> NewestFirst<T>(IEnumerable<T> playables) where T : Playable =>
        playables
            .OrderByDescending(p => p.Release?.ToSortUtc() ?? DateTime.MinValue)
            .ThenBy(p => p.Id);

    private static SortedDictionary<string, Uri> ToUrls(Dictionary<string, ServiceLink>? services)
    {
        var urls = new SortedDictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var (key, link) in services ?? [])
        {
            if (link.Url is not null)
            {
                urls[key] = link.Url;
            }
        }

        return urls;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string>? NullIfEmpty(List<string> values) => values.Count > 0 ? values : null;
}
