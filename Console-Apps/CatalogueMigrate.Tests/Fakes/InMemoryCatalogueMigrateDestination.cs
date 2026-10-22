using CatalogueMigrate;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace CatalogueMigrate.Tests.Fakes;

/// <summary>Synthetic in-memory destination containers and search index for migrate rules.</summary>
public sealed class InMemoryCatalogueMigrateDestination : ICatalogueMigrateDestination
{
    public Dictionary<Guid, NewsOrganisation> NewsOrganisations { get; } = new();
    public Dictionary<(Guid Parent, Guid Id), NewsReport> NewsReports { get; } = new();
    public Dictionary<Guid, TvShow> TvShows { get; } = new();
    public Dictionary<(Guid Parent, Guid Id), TvShowEpisode> TvShowEpisodes { get; } = new();
    public Dictionary<Guid, Film> Films { get; } = new();
    public Dictionary<string, EpisodeSearchRecord> Search { get; } = new();
    public int SearchUploadCalls { get; private set; }

    /// <summary>When set, the next search uploads fail (returns false).</summary>
    public bool FailSearch { get; set; }

    /// <summary>Runs after a successful search upload: something else writing the source mid-move.</summary>
    public Func<Task>? AfterSearchUpload { get; set; }

    public Task<bool> HasTvShow(Guid tvShowId) => Task.FromResult(TvShows.ContainsKey(tvShowId));

    public Task<bool> HasNewsOrganisation(Guid newsOrganisationId) =>
        Task.FromResult(NewsOrganisations.ContainsKey(newsOrganisationId));

    public Task SaveNews(NewsOrganisation organisation, IReadOnlyList<NewsReport> reports)
    {
        NewsOrganisations[organisation.Id] = organisation;
        foreach (var report in reports)
        {
            NewsReports[(organisation.Id, report.Id)] = report;
        }

        return Task.CompletedTask;
    }

    public Task SaveTv(TvShow show, IReadOnlyList<TvShowEpisode> episodes)
    {
        TvShows[show.Id] = show;
        foreach (var episode in episodes)
        {
            TvShowEpisodes[(show.Id, episode.Id)] = episode;
        }

        return Task.CompletedTask;
    }

    public Task SaveFilm(Film film)
    {
        Films[film.Id] = film;
        return Task.CompletedTask;
    }

    public Task<int> CountPlayables(string kind, Guid parentId) => Task.FromResult(kind switch
    {
        SubmitClassification.NewsReport => NewsReports.Keys.Count(key => key.Parent == parentId),
        SubmitClassification.TvShowEpisode => TvShowEpisodes.Keys.Count(key => key.Parent == parentId),
        _ => Films.ContainsKey(parentId) ? 1 : 0
    });

    public Task<Guid?> FindOtherParentWithName(string kind, string name, string fileKey, Guid excludeId) =>
        Task.FromResult(kind switch
        {
            SubmitClassification.NewsReport => NewsOrganisations.Values
                .FirstOrDefault(x => (x.Name == name || x.FileKey == fileKey) && x.Id != excludeId)?.Id,
            SubmitClassification.TvShowEpisode => TvShows.Values
                .FirstOrDefault(x => (x.Name == name || x.FileKey == fileKey) && x.Id != excludeId)?.Id,
            _ => (Guid?)null
        });

    public Task DeletePlayable(string kind, Guid parentId, Guid playableId)
    {
        switch (kind)
        {
            case SubmitClassification.NewsReport:
                NewsReports.Remove((parentId, playableId));
                break;
            case SubmitClassification.TvShowEpisode:
                TvShowEpisodes.Remove((parentId, playableId));
                break;
            default:
                Films.Remove(playableId);
                break;
        }

        return Task.CompletedTask;
    }

    public Task DeleteParent(string kind, Guid parentId)
    {
        if (kind == SubmitClassification.NewsReport)
        {
            NewsOrganisations.Remove(parentId);
        }
        else if (kind == SubmitClassification.TvShowEpisode)
        {
            TvShows.Remove(parentId);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> UploadSearch(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken)
    {
        SearchUploadCalls++;
        if (FailSearch)
        {
            return false;
        }

        foreach (var document in documents)
        {
            Search[document.Id] = document;
        }

        if (AfterSearchUpload is { } after)
        {
            await after();
        }

        return true;
    }
}
