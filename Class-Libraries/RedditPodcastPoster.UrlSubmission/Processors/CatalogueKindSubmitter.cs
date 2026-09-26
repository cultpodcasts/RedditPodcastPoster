using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public class CatalogueKindSubmitter(
    IOptions<SubmitContentTypesOptions> submitContentTypes,
    IFilmRepository films,
    ITvShowRepository tvShows,
    ITvShowEpisodeRepository tvShowEpisodes,
    INewsOrganisationRepository newsOrganisations,
    INewsReportRepository newsReports,
    INonPodcastServiceAdapterResolver adapters) : ICatalogueKindSubmitter
{
    public async Task<SubmitResult?> TrySubmit(CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
        if (submitContentTypes.Value is not { Enabled: true } || submitOptions.ClassificationSignals is null)
        {
            return null;
        }

        var classified = SubmitContentClassifier.Classify(submitOptions.ClassificationSignals);
        if (classified.Reject || classified.RequiresCurator)
        {
            return new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: classified.ContentKind,
                Rejected: classified.Reject,
                RequiresCurator: classified.RequiresCurator);
        }

        if (classified.ContentKind == SubmitClassification.Episode)
        {
            return null;
        }

        if (categorisedItem.ResolvedNonPodcastServiceItem is not { } source)
        {
            return new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: classified.ContentKind);
        }

        // A podcast Episode that already owns this URL must not gain a Film, TvShow, or NewsReport sibling.
        if (categorisedItem.MatchingEpisode is { } existingEpisode)
        {
            return AlreadyExists(SubmitClassification.Episode, existingEpisode.Id);
        }

        if (!submitOptions.PersistToDatabase)
        {
            return new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                ContentKind: classified.ContentKind);
        }

        if (classified.ContentKind == SubmitClassification.Film)
        {
            var existing = await FindByCanonicalUrl(films, source);
            if (existing != null)
            {
                return AlreadyExists(SubmitClassification.Film, existing.Id);
            }

            var film = CreateFilm(source);
            await films.Save(film);
            return CreatedPlayable(SubmitClassification.Film, film.Id, parentCreated: false);
        }

        if (classified.ContentKind == SubmitClassification.TvShowEpisode)
        {
            var existing = await FindByCanonicalUrl(tvShowEpisodes, source);
            if (existing != null)
            {
                return AlreadyExists(SubmitClassification.TvShowEpisode, existing.Id);
            }

            var saved = await SaveTvShowEpisode(source, submitOptions);
            return CreatedPlayable(SubmitClassification.TvShowEpisode, saved.Episode.Id, saved.ParentCreated);
        }

        if (classified.ContentKind == SubmitClassification.NewsReport)
        {
            var existing = await FindByCanonicalUrl(newsReports, source);
            if (existing != null)
            {
                return AlreadyExists(SubmitClassification.NewsReport, existing.Id);
            }

            var saved = await SaveNewsReport(source, submitOptions);
            return CreatedPlayable(SubmitClassification.NewsReport, saved.Report.Id, saved.ParentCreated);
        }

        return null;
    }

    private static SubmitResult AlreadyExists(string contentKind, Guid playableId) =>
        new(
            SubmitResultState.EpisodeAlreadyExists,
            SubmitResultState.None,
            ContentKind: contentKind,
            PlayableId: playableId);

    private static SubmitResult CreatedPlayable(string contentKind, Guid playableId, bool parentCreated) =>
        new(
            SubmitResultState.Created,
            parentCreated ? SubmitResultState.Created : SubmitResultState.None,
            ContentKind: contentKind,
            PlayableId: playableId);

    private Film CreateFilm(ResolvedNonPodcastServiceItem source)
    {
        var film = new Film(RequiredTitle(source))
        {
            Description = source.Description ?? string.Empty,
            Length = source.Duration ?? TimeSpan.Zero,
            Explicit = source.Explicit,
            Services = ServiceMap(source)
        };
        if (source.Release is { } release)
        {
            film.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        }

        return film;
    }

    private async Task<(TvShowEpisode Episode, bool ParentCreated)> SaveTvShowEpisode(
        ResolvedNonPodcastServiceItem source,
        SubmitOptions submitOptions)
    {
        var name = TvSeriesName(source, submitOptions);
        var parents = await PublisherNameAttachLookup.FindByName(tvShows, name);
        if (parents.Count > 1)
        {
            throw new AmbiguousParentNameException(
                SubmitClassification.TvShowEpisode, name, parents.Select(show => show.Id).ToArray());
        }

        var show = parents.Count == 1 ? parents[0] : new TvShow(name);
        if (parents.Count == 0)
        {
            await tvShows.Save(show);
        }

        var episode = new TvShowEpisode(RequiredTitle(source))
        {
            Description = source.Description ?? string.Empty,
            Length = source.Duration ?? TimeSpan.Zero,
            Explicit = source.Explicit,
            Services = ServiceMap(source)
        };
        episode.SetTvShowProperties(show);
        if (source.Release is { } release)
        {
            episode.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        }

        await tvShowEpisodes.Save(episode);
        return (episode, parents.Count == 0);
    }

    private async Task<(NewsReport Report, bool ParentCreated)> SaveNewsReport(
        ResolvedNonPodcastServiceItem source,
        SubmitOptions submitOptions)
    {
        var name = NewsOutletName(source, submitOptions);
        var parents = await PublisherNameAttachLookup.FindByName(newsOrganisations, name);
        if (parents.Count > 1)
        {
            throw new AmbiguousParentNameException(
                SubmitClassification.NewsReport, name, parents.Select(org => org.Id).ToArray());
        }

        var organisation = parents.Count == 1 ? parents[0] : new NewsOrganisation(name);
        if (parents.Count == 0)
        {
            await newsOrganisations.Save(organisation);
        }

        var report = new NewsReport(RequiredTitle(source))
        {
            Description = source.Description ?? string.Empty,
            Length = source.Duration ?? TimeSpan.Zero,
            Explicit = source.Explicit,
            Services = ServiceMap(source)
        };
        report.SetNewsOrganisationProperties(organisation);
        if (source.Release is { } release)
        {
            report.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        }

        await newsReports.Save(report);
        return (report, parents.Count == 0);
    }

    private async Task<T?> FindByCanonicalUrl<T>(
        IFilterableRepository<T> repository,
        ResolvedNonPodcastServiceItem source)
        where T : class, IPlayable
    {
        if (source.Url is null)
        {
            return null;
        }

        var serviceKey = StreamingServiceWire.ToKey(source.StreamingService);
        var canonical = CanonicalStoredUrl(source.Url);
        return await repository.GetBy(item =>
            item.Services != null && item.Services[serviceKey].Url == canonical);
    }

    private Dictionary<string, ServiceLink> ServiceMap(ResolvedNonPodcastServiceItem source)
    {
        if (source.Url is null)
        {
            return new Dictionary<string, ServiceLink>();
        }

        var link = new ServiceLink { Url = CanonicalStoredUrl(source.Url) };
        if (source.Image is not null)
        {
            link.Image = source.Image;
        }

        return new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(source.StreamingService)] = link
        };
    }

    private Uri CanonicalStoredUrl(Uri url) =>
        adapters.ForSubmit(url)?.CanonicalStoredUrl(url) ?? url;

    private static string TvSeriesName(ResolvedNonPodcastServiceItem source, SubmitOptions submitOptions)
    {
        var name = FirstNonEmpty(source.ShowName, submitOptions.PodcastName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "TV submit needs a series name before it can be stored.");
        }

        return name.Trim();
    }

    private static string NewsOutletName(ResolvedNonPodcastServiceItem source, SubmitOptions submitOptions)
    {
        var name = FirstNonEmpty(source.ShowName, source.Publisher, submitOptions.PodcastName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "News submit needs an organisation name before it can be stored.");
        }

        return name.Trim();
    }

    private static string RequiredTitle(ResolvedNonPodcastServiceItem source)
    {
        if (string.IsNullOrWhiteSpace(source.Title))
        {
            throw new InvalidOperationException("Film, TV, and News submit need a title.");
        }

        return source.Title.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
