using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace CatalogueMigrate;

public sealed class RepositoryCatalogueMigrateDestinationReader(
    ITvShowRepository tvShows,
    ITvShowEpisodeRepository tvShowEpisodes,
    INewsOrganisationRepository newsOrganisations,
    INewsReportRepository newsReports,
    IFilmRepository films) : ICatalogueMigrateDestinationReader
{
    public async Task<int> CountPlayables(string kind, Guid parentId) => kind switch
    {
        SubmitClassification.NewsReport => await newsReports.Count(parentId),
        SubmitClassification.TvShowEpisode => await tvShowEpisodes.Count(parentId),
        SubmitClassification.Film => await films.GetFilm(parentId) is null ? 0 : 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public async Task<Guid?> FindOtherParentWithName(string kind, string name, string fileKey, Guid excludeId) =>
        kind switch
        {
            SubmitClassification.NewsReport => (await newsOrganisations.GetBy(x =>
                (x.Name == name || x.FileKey == fileKey) && x.Id != excludeId))?.Id,
            SubmitClassification.TvShowEpisode => (await tvShows.GetBy(x =>
                (x.Name == name || x.FileKey == fileKey) && x.Id != excludeId))?.Id,
            SubmitClassification.Film => null,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
