using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace CatalogueMigrate;

public sealed class RepositoryCatalogueMigrateDestination(
    ITvShowRepository tvShows,
    ITvShowEpisodeRepository tvShowEpisodes,
    INewsOrganisationRepository newsOrganisations,
    INewsReportRepository newsReports,
    IFilmRepository films,
    IPlayableSearchDocumentSwap searchSwap) : ICatalogueMigrateDestination
{
    public async Task<bool> HasTvShow(Guid tvShowId) => await tvShows.GetTvShow(tvShowId) is not null;

    public async Task<bool> HasNewsOrganisation(Guid newsOrganisationId) =>
        await newsOrganisations.GetNewsOrganisation(newsOrganisationId) is not null;

    public async Task SaveNews(NewsOrganisation organisation, IReadOnlyList<NewsReport> reports)
    {
        await newsOrganisations.Save(organisation);
        if (reports.Count > 0)
        {
            await newsReports.Save(reports);
        }
    }

    public async Task SaveTv(TvShow show, IReadOnlyList<TvShowEpisode> episodes)
    {
        await tvShows.Save(show);
        if (episodes.Count > 0)
        {
            await tvShowEpisodes.Save(episodes);
        }
    }

    public Task SaveFilm(Film film) => films.Save(film);

    public async Task<int> CountPlayables(string kind, Guid parentId) => kind switch
    {
        SubmitClassification.NewsReport => await newsReports.Count(parentId),
        SubmitClassification.TvShowEpisode => await tvShowEpisodes.Count(parentId),
        SubmitClassification.Film => await films.GetFilm(parentId) is null ? 0 : 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public Task DeletePlayable(string kind, Guid parentId, Guid playableId) => kind switch
    {
        SubmitClassification.NewsReport => newsReports.Delete(parentId, playableId),
        SubmitClassification.TvShowEpisode => tvShowEpisodes.Delete(parentId, playableId),
        SubmitClassification.Film => films.Delete(playableId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public Task DeleteParent(string kind, Guid parentId) => kind switch
    {
        SubmitClassification.NewsReport => newsOrganisations.Delete(parentId),
        SubmitClassification.TvShowEpisode => tvShows.Delete(parentId),
        SubmitClassification.Film => Task.CompletedTask,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public Task<bool> UploadSearch(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken) =>
        searchSwap.UploadAsync(documents, cancellationToken);
}
