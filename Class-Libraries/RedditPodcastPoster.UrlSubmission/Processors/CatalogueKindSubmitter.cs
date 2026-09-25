using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public class CatalogueKindSubmitter(
    IOptions<SubmitContentTypesOptions>? submitContentTypes,
    IFilmRepository? films,
    ITvShowRepository? tvShows,
    ITvShowEpisodeRepository? tvShowEpisodes,
    INewsOrganisationRepository? newsOrganisations,
    INewsReportRepository? newsReports) : ICatalogueKindSubmitter
{
    public async Task<SubmitResult?> TrySubmit(CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
        if (submitContentTypes?.Value?.Enabled != true || submitOptions.ClassificationSignals is null)
        {
            return null;
        }

        var classified = SubmitContentClassifier.Classify(submitOptions.ClassificationSignals);
        if (classified.Reject || classified.RequiresCurator)
        {
            return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
        }

        if (classified.ContentKind == SubmitClassification.Episode)
        {
            return null;
        }

        if (!submitOptions.PersistToDatabase)
        {
            return new SubmitResult(SubmitResultState.Created, SubmitResultState.None);
        }

        var source = categorisedItem.ResolvedNonPodcastServiceItem
            ?? throw new InvalidOperationException(
                "A Film, TV, or News submit needs a resolved non-podcast item.");

        if (classified.ContentKind == SubmitClassification.Film)
        {
            if (films is null)
            {
                throw new InvalidOperationException("Film submit is enabled but no film repository is registered.");
            }

            var film = CreateFilm(source);
            await films.Save(film);
            return CreatedPlayable(SubmitClassification.Film, film.Id, parentCreated: false);
        }

        if (classified.ContentKind == SubmitClassification.TvShowEpisode)
        {
            var saved = await SaveTvShowEpisode(source, submitOptions);
            return CreatedPlayable(SubmitClassification.TvShowEpisode, saved.Episode.Id, saved.ParentCreated);
        }

        if (classified.ContentKind == SubmitClassification.NewsReport)
        {
            var saved = await SaveNewsReport(source, submitOptions);
            return CreatedPlayable(SubmitClassification.NewsReport, saved.Report.Id, saved.ParentCreated);
        }

        return null;
    }

    private static SubmitResult CreatedPlayable(string contentKind, Guid playableId, bool parentCreated) =>
        new(
            SubmitResultState.Created,
            parentCreated ? SubmitResultState.Created : SubmitResultState.None,
            ContentKind: contentKind,
            PlayableId: playableId);

    private static Film CreateFilm(ResolvedNonPodcastServiceItem source)
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

    private async Task<(TvShowEpisode Episode, bool ParentCreated)> SaveTvShowEpisode(ResolvedNonPodcastServiceItem source, SubmitOptions submitOptions)
    {
        if (tvShows is null || tvShowEpisodes is null)
        {
            throw new InvalidOperationException("TV submit is enabled but TV repositories are not registered.");
        }

        var name = ParentName(source, submitOptions);
        var parents = await tvShows.GetAllBy(show => show.Name == name).ToListAsync();
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

    private async Task<(NewsReport Report, bool ParentCreated)> SaveNewsReport(ResolvedNonPodcastServiceItem source, SubmitOptions submitOptions)
    {
        if (newsOrganisations is null || newsReports is null)
        {
            throw new InvalidOperationException("News submit is enabled but news repositories are not registered.");
        }

        var name = ParentName(source, submitOptions);
        var parents = await newsOrganisations.GetAllBy(org => org.Name == name).ToListAsync();
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

    private static string ParentName(ResolvedNonPodcastServiceItem source, SubmitOptions submitOptions)
    {
        var name = FirstNonEmpty(source.ShowName, source.Publisher, submitOptions.PodcastName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "TV and News submit need a series or organisation name before they can be stored.");
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

    private static Dictionary<string, ServiceLink> ServiceMap(ResolvedNonPodcastServiceItem source)
    {
        if (source.Url is null)
        {
            return new Dictionary<string, ServiceLink>();
        }

        return new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(source.StreamingService)] = new() { Url = source.Url }
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
