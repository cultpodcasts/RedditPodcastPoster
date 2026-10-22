using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

/// <summary>
/// Reads recent news reports, TV show episodes and films straight from their containers with a release
/// filter. It does not go through the parents' <c>latestReleased</c> the way the podcast path does,
/// because a submitted news report or TV episode does not move its parent's <c>latestReleased</c>
/// (only an Indexer pass does), so a parent filter would hide fresh submits.
/// The week's rows are few, so they are read whole and mapped with the same projection social posting uses.
/// Each parent is then read once by id: its current name, regexes and known terms are used, and a row
/// whose parent is missing or removed is dropped even if its denormalised <c>parentRemoved</c> is stale.
/// </summary>
public class HomepageCataloguePlayables(
    INewsReportRepository newsReportRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    ITvShowRepository tvShowRepository,
    IFilmRepository filmRepository,
    ILogger<HomepageCataloguePlayables> logger)
    : IHomepageCataloguePlayables
{
    public async Task<IReadOnlyList<HomepageCataloguePlayable>> GetRecent(DateTime since, CancellationToken ct)
    {
        var newsTask = GetRecentNewsReports(since, ct);
        var tvTask = GetRecentTvShowEpisodes(since, ct);
        var filmsTask = GetRecentFilms(since, ct);
        await Task.WhenAll(newsTask, tvTask, filmsTask);

        var items = newsTask.Result.Concat(tvTask.Result).Concat(filmsTask.Result).ToList();
        logger.LogInformation(
            "Homepage catalogue playables since '{Since:O}': news-reports {News}, tv-show-episodes {Tv}, films {Films}.",
            since, newsTask.Result.Count, tvTask.Result.Count, filmsTask.Result.Count);
        return items;
    }

    public async Task<IReadOnlyList<TimeSpan>> GetActiveDurations(CancellationToken ct)
    {
        var news = newsReportRepository
            .GetAllBy(
                x => !x.Removed && !x.Ignored &&
                     (!x.ParentRemoved.IsDefined() || x.ParentRemoved == null || x.ParentRemoved == false),
                x => x.Length)
            .ToListAsync(ct)
            .AsTask();
        var tv = tvShowEpisodeRepository
            .GetAllBy(
                x => !x.Removed && !x.Ignored &&
                     (!x.ParentRemoved.IsDefined() || x.ParentRemoved == null || x.ParentRemoved == false),
                x => x.Length)
            .ToListAsync(ct)
            .AsTask();
        var films = filmRepository
            .GetAllBy(
                x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) && !x.Ignored,
                x => x.Length)
            .ToListAsync(ct)
            .AsTask();
        await Task.WhenAll(news, tv, films);
        return [.. news.Result, .. tv.Result, .. films.Result];
    }

    public async Task<int> CountActive(CancellationToken ct)
    {
        var news = newsReportRepository
            .GetAllBy(
                x => !x.Removed &&
                     (!x.ParentRemoved.IsDefined() || x.ParentRemoved == null || x.ParentRemoved == false),
                x => x.Id)
            .ToListAsync(ct)
            .AsTask();
        var tv = tvShowEpisodeRepository
            .GetAllBy(
                x => !x.Removed &&
                     (!x.ParentRemoved.IsDefined() || x.ParentRemoved == null || x.ParentRemoved == false),
                x => x.Id)
            .ToListAsync(ct)
            .AsTask();
        var films = filmRepository
            .GetAllBy(
                x => !x.Removed.IsDefined() || x.Removed == null || x.Removed == false,
                x => x.Id)
            .ToListAsync(ct)
            .AsTask();
        await Task.WhenAll(news, tv, films);
        return news.Result.Count + tv.Result.Count + films.Result.Count;
    }

    private async Task<List<HomepageCataloguePlayable>> GetRecentNewsReports(DateTime since, CancellationToken ct)
    {
        var reports = await newsReportRepository
            .GetAllBy(x =>
                (x.ReleaseSort.IsDefined() ? x.ReleaseSort : x.ReleaseCosmosFallback) >= since &&
                !x.Removed && !x.Ignored &&
                (!x.ParentRemoved.IsDefined() || x.ParentRemoved == null || x.ParentRemoved == false))
            .ToListAsync(ct);

        var organisations = new Dictionary<Guid, NewsOrganisation?>();
        foreach (var organisationId in reports.Select(x => x.NewsOrganisationId).Distinct())
        {
            ct.ThrowIfCancellationRequested();
            organisations[organisationId] = await newsOrganisationRepository.GetNewsOrganisation(organisationId);
        }

        var items = new List<HomepageCataloguePlayable>();
        foreach (var report in reports.Where(x => IsLive(x, since)))
        {
            if (organisations.GetValueOrDefault(report.NewsOrganisationId) is not { } organisation ||
                organisation.IsRemoved())
            {
                continue;
            }

            var projection = CatalogueParentKindMapper.ToPostProjection(report, organisation);
            items.Add(ToItem(
                nameof(ContentKind.NewsReport), projection.Episode, organisation.Name, organisation,
                organisation.Indexing));
        }

        return items;
    }

    private async Task<List<HomepageCataloguePlayable>> GetRecentTvShowEpisodes(DateTime since, CancellationToken ct)
    {
        var episodes = await tvShowEpisodeRepository
            .GetAllBy(x =>
                (x.ReleaseSort.IsDefined() ? x.ReleaseSort : x.ReleaseCosmosFallback) >= since &&
                !x.Removed && !x.Ignored &&
                (!x.ParentRemoved.IsDefined() || x.ParentRemoved == null || x.ParentRemoved == false))
            .ToListAsync(ct);

        var shows = new Dictionary<Guid, TvShow?>();
        foreach (var showId in episodes.Select(x => x.TvShowId).Distinct())
        {
            ct.ThrowIfCancellationRequested();
            shows[showId] = await tvShowRepository.GetTvShow(showId);
        }

        var items = new List<HomepageCataloguePlayable>();
        foreach (var episode in episodes.Where(x => IsLive(x, since)))
        {
            if (shows.GetValueOrDefault(episode.TvShowId) is not { } show || show.IsRemoved())
            {
                continue;
            }

            var projection = CatalogueParentKindMapper.ToPostProjection(episode, show);
            items.Add(ToItem(nameof(ContentKind.TvShowEpisode), projection.Episode, show.Name, show, show.Indexing));
        }

        return items;
    }

    private async Task<List<HomepageCataloguePlayable>> GetRecentFilms(DateTime since, CancellationToken ct)
    {
        // A film written before releaseSort existed only has release (no releaseCosmosFallback member on Film),
        // and its ReleaseSort is rebuilt from that on read: fetch those too and apply the window in memory
        // (same as CataloguePlayableSocialPosts).
        var films = await filmRepository
            .GetAllBy(x =>
                (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                (!x.ReleaseSort.IsDefined() || x.ReleaseSort >= since))
            .ToListAsync(ct);

        var items = new List<HomepageCataloguePlayable>();
        foreach (var film in films.Where(x => !x.IsRemoved() && !x.Ignored && x.ReleaseSort >= since))
        {
            var projection = CatalogueParentKindMapper.ToPostProjection(film);
            items.Add(ToItem(nameof(ContentKind.Film), projection.Episode, film.Name, film, indexing: null));
        }

        return items;
    }

    private static bool IsLive(Playable playable, DateTime since) =>
        !playable.Removed && !playable.Ignored && playable.ParentRemoved != true && playable.ReleaseSort >= since;

    private static HomepageCataloguePlayable ToItem(
        string contentKind,
        Episode episode,
        string parentName,
        Publisher publisher,
        CatalogueIndexingSource? indexing)
    {
        EpisodeServicePresence.NormalizeCatalog(episode);
        return new HomepageCataloguePlayable
        {
            ContentKind = contentKind,
            Id = episode.Id,
            ParentName = parentName.Trim(),
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.ReleaseUtc,
            Length = episode.Length,
            Services = episode.Services,
            Ids = episode.Ids,
            Images = EpisodeServicePresence.ToEpisodeImages(episode),
            Subjects = episode.Subjects,
            Language = episode.Language,
            TitleRegex = indexing?.TitleRegex ?? string.Empty,
            DescriptionRegex = indexing?.DescriptionRegex ?? string.Empty,
            KnownTerms = publisher.KnownTerms
        };
    }
}
