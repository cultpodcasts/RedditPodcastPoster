using RedditPodcastPoster.Catalogue.Authority;
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
using RedditPodcastPoster.UrlSubmission.Languages;
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
    INonPodcastServiceAdapterResolver adapters,
    ICatalogueLanguageGate languageGate,
    ICatalogueIdentityEnricher identityEnricher) : ICatalogueKindSubmitter
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

        if (!submitOptions.PersistToDatabase &&
            !submitOptions.EnforceLanguageGate &&
            !submitOptions.LanguageProbe &&
            submitOptions.LanguageChoice != CatalogueLanguageChoice.Cancel)
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

            var filmAdmission = await AdmitOrStop(
                CatalogueLanguageTextReader.ForParentAndChild(
                    source.Title, source.Description, source.Title, source.Description, "Film", "Film"),
                creatingNewParent: true,
                submitOptions,
                SubmitClassification.Film);
            if (filmAdmission.Stop != null)
            {
                return filmAdmission.Stop;
            }

            var filmIdentity = await AdmitIdentityFilm(source, submitOptions, SubmitClassification.Film);
            if (filmIdentity.Stop != null)
            {
                return filmIdentity.Stop;
            }

            if (submitOptions.LanguageProbe)
            {
                return LanguageProbeResult(
                    SubmitClassification.Film,
                    filmAdmission.Admission.SetsDefaultLanguage);
            }

            var film = CreateFilm(source);
            if (filmAdmission.Admission.LanguageCode != null)
            {
                film.Language = filmAdmission.Admission.LanguageCode;
            }

            identityEnricher.ApplyMissing(film, filmIdentity.Admission.Ids);
            await films.Save(film);
            return CreatedPlayable(
                SubmitClassification.Film,
                film.Id,
                parentCreated: false,
                filmAdmission.Admission.SetsDefaultLanguage);
        }

        if (classified.ContentKind == SubmitClassification.TvShowEpisode)
        {
            var existing = await FindByCanonicalUrl(tvShowEpisodes, source);
            if (existing != null)
            {
                return AlreadyExists(SubmitClassification.TvShowEpisode, existing.Id);
            }

            var saved = await SaveTvShowEpisode(source, submitOptions);
            if (saved.Stop != null)
            {
                return saved.Stop;
            }

            return CreatedPlayable(
                SubmitClassification.TvShowEpisode,
                saved.Episode!.Id,
                saved.ParentCreated,
                saved.SetsDefaultLanguage,
                saved.SearchSource);
        }

        if (classified.ContentKind == SubmitClassification.NewsReport)
        {
            var existing = await FindByCanonicalUrl(newsReports, source);
            if (existing != null)
            {
                return AlreadyExists(SubmitClassification.NewsReport, existing.Id);
            }

            var saved = await SaveNewsReport(source, submitOptions);
            if (saved.Stop != null)
            {
                return saved.Stop;
            }

            return CreatedPlayable(
                SubmitClassification.NewsReport,
                saved.Report!.Id,
                saved.ParentCreated,
                saved.SetsDefaultLanguage,
                saved.SearchSource);
        }

        return null;
    }

    private static SubmitResult AlreadyExists(string contentKind, Guid playableId) =>
        new(
            SubmitResultState.EpisodeAlreadyExists,
            SubmitResultState.None,
            ContentKind: contentKind,
            PlayableId: playableId);

    private static SubmitResult CreatedPlayable(
        string contentKind,
        Guid playableId,
        bool parentCreated,
        bool defaultLanguageFromDetection = false,
        CatalogueSearchSource? searchSource = null) =>
        new(
            SubmitResultState.Created,
            parentCreated ? SubmitResultState.Created : SubmitResultState.None,
            ContentKind: contentKind,
            PlayableId: playableId,
            CatalogueSearchSource: searchSource,
            DefaultLanguageFromDetection: defaultLanguageFromDetection);

    private Film CreateFilm(ResolvedNonPodcastServiceItem source)
    {
        var film = new Film(RequiredTitle(source))
        {
            Description = source.Description ?? string.Empty,
            Length = source.Duration ?? TimeSpan.Zero,
            Explicit = source.Explicit,
            Services = ServiceMap(source),
            PublisherName = TrimmedOrEmpty(source.Publisher)
        };
        if (source.Release is { } release)
        {
            film.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        }

        return film;
    }

    private async Task<(TvShowEpisode? Episode, bool ParentCreated, SubmitResult? Stop, bool SetsDefaultLanguage, CatalogueSearchSource? SearchSource)> SaveTvShowEpisode(
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

        var creatingParent = parents.Count == 0;
        var show = creatingParent
            ? new TvShow(name) { PublisherName = TrimmedOrEmpty(source.Publisher) }
            : parents[0];
        var admitted = await AdmitOrStop(
            CatalogueLanguageTextReader.ForParentAndChild(
                show.Name,
                creatingParent ? null : show.Description,
                source.Title,
                source.Description,
                "TV show",
                "Episode"),
            creatingParent,
            submitOptions,
            SubmitClassification.TvShowEpisode);
        if (admitted.Stop != null)
        {
            return (null, creatingParent, admitted.Stop, false, null);
        }

        var identity = await AdmitIdentityTv(show, RequiredTitle(source), submitOptions, SubmitClassification.TvShowEpisode);
        if (identity.Stop != null)
        {
            return (null, creatingParent, identity.Stop, false, null);
        }

        if (submitOptions.LanguageProbe)
        {
            return (
                null,
                creatingParent,
                LanguageProbeResult(
                    SubmitClassification.TvShowEpisode,
                    admitted.Admission.SetsDefaultLanguage),
                false,
                null);
        }

        if (creatingParent)
        {
            if (admitted.Admission.LanguageCode != null)
            {
                show.Language = admitted.Admission.LanguageCode;
            }

            identityEnricher.ApplyMissing(show, identity.Admission.ParentIds ?? identity.Admission.Ids);
            await tvShows.Save(show);
        }
        else if (identityEnricher.ApplyMissing(show, identity.Admission.ParentIds))
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
        if (creatingParent && admitted.Admission.LanguageCode != null &&
            string.IsNullOrWhiteSpace(episode.Language))
        {
            episode.Language = admitted.Admission.LanguageCode;
        }

        if (source.Release is { } release)
        {
            episode.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        }

        identityEnricher.ApplyMissing(episode, identity.Admission.Ids);
        await tvShowEpisodes.Save(episode);
        return (
            episode,
            creatingParent,
            null,
            admitted.Admission.SetsDefaultLanguage,
            ImmediateSearchSource(show, episode, CatalogueParentKind.TvShow));
    }

    private async Task<(NewsReport? Report, bool ParentCreated, SubmitResult? Stop, bool SetsDefaultLanguage, CatalogueSearchSource? SearchSource)> SaveNewsReport(
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

        var creatingParent = parents.Count == 0;
        var organisation = creatingParent
            ? new NewsOrganisation(name) { PublisherName = TrimmedOrEmpty(source.Publisher) }
            : parents[0];
        var admitted = await AdmitOrStop(
            CatalogueLanguageTextReader.ForParentAndChild(
                organisation.Name,
                creatingParent ? null : organisation.Description,
                source.Title,
                source.Description,
                "News organisation",
                "Report"),
            creatingParent,
            submitOptions,
            SubmitClassification.NewsReport);
        if (admitted.Stop != null)
        {
            return (null, creatingParent, admitted.Stop, false, null);
        }

        if (submitOptions.LanguageProbe)
        {
            return (
                null,
                creatingParent,
                LanguageProbeResult(
                    SubmitClassification.NewsReport,
                    admitted.Admission.SetsDefaultLanguage),
                false,
                null);
        }

        if (creatingParent)
        {
            if (admitted.Admission.LanguageCode != null)
            {
                organisation.Language = admitted.Admission.LanguageCode;
            }

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
        if (creatingParent && admitted.Admission.LanguageCode != null &&
            string.IsNullOrWhiteSpace(report.Language))
        {
            report.Language = admitted.Admission.LanguageCode;
        }

        if (source.Release is { } release)
        {
            report.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        }

        await newsReports.Save(report);
        return (
            report,
            creatingParent,
            null,
            admitted.Admission.SetsDefaultLanguage,
            ImmediateSearchSource(organisation, report, CatalogueParentKind.NewsOrganisation));
    }

    /// <summary>
    /// Same transient-podcast projection <see cref="CatalogueParentSubmitter"/> uses so the host can upload
    /// the search document immediately. Kind-classified creates previously returned only a playable id, so
    /// <c>SubmitUrlService</c> exited without indexing and the item waited for the pull indexer.
    /// </summary>
    private static CatalogueSearchSource ImmediateSearchSource(
        Publisher parent,
        Playable playable,
        CatalogueParentKind kind)
    {
        var indexing = parent switch
        {
            TvShow show => show.Indexing,
            NewsOrganisation organisation => organisation.Indexing,
            _ => null
        };
        var transient = (indexing ?? new CatalogueIndexingSource()).ToTransientPodcast(parent);
        return new CatalogueSearchSource(
            transient,
            CatalogueParentKnownEpisodes.ToEpisode(playable, transient),
            kind);
    }

    private static string TrimmedOrEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private async Task<(CatalogueLanguageAdmission Admission, SubmitResult? Stop)> AdmitOrStop(
        CatalogueLanguageTexts texts,
        bool creatingNewParent,
        SubmitOptions options,
        string contentKind)
    {
        if (options.LanguageChoice == CatalogueLanguageChoice.Cancel)
        {
            return (CatalogueLanguageAdmission.NeedsPrompt(new CatalogueLanguageHold(string.Empty, string.Empty, [])),
                new SubmitResult(SubmitResultState.None, SubmitResultState.None, ContentKind: contentKind,
                    LanguageHold: new CatalogueLanguageHold(string.Empty, string.Empty, [])));
        }

        if (!options.EnforceLanguageGate && !options.LanguageProbe)
        {
            return (CatalogueLanguageAdmission.Proceed(), null);
        }

        var admission = options.EnforceLanguageGate
            ? await languageGate.Evaluate(texts, creatingNewParent, options)
            : CatalogueLanguageAdmission.Proceed();
        if (admission.Hold)
        {
            return (admission, new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: contentKind,
                LanguageHold: admission.Prompt));
        }

        // LanguageProbe continues so identity can also be checked before the no-write return.
        return (admission, null);
    }

    private async Task<(CatalogueIdentityAdmission Admission, SubmitResult? Stop)> AdmitIdentityFilm(
        ResolvedNonPodcastServiceItem source,
        SubmitOptions options,
        string contentKind)
    {
        var admission = await identityEnricher.ResolveFilmAsync(
            RequiredTitle(source),
            source.Release?.Year,
            options.IdentityChoice,
            options.SelectedIdentityKey).ConfigureAwait(false);
        return IdentityStop(admission, contentKind);
    }

    private async Task<(CatalogueIdentityAdmission Admission, SubmitResult? Stop)> AdmitIdentityTv(
        TvShow show,
        string episodeTitle,
        SubmitOptions options,
        string contentKind)
    {
        var admission = await identityEnricher.ResolveTvShowEpisodeAsync(
            show,
            episodeTitle,
            options.IdentityChoice,
            options.SelectedIdentityKey);
        return IdentityStop(admission, contentKind);
    }

    private static (CatalogueIdentityAdmission Admission, SubmitResult? Stop) IdentityStop(
        CatalogueIdentityAdmission admission,
        string contentKind)
    {
        if (!admission.Hold)
        {
            return (admission, null);
        }

        return (admission, new SubmitResult(
            SubmitResultState.None,
            SubmitResultState.None,
            ContentKind: contentKind,
            IdentityHold: admission.Prompt));
    }

    private static SubmitResult LanguageProbeResult(
        string contentKind,
        bool setsDefaultLanguage,
        CatalogueIdentityHold? identityHold = null) =>
        new(
            SubmitResultState.None,
            SubmitResultState.None,
            ContentKind: contentKind,
            DefaultLanguageFromDetection: setsDefaultLanguage,
            IdentityHold: identityHold);

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
