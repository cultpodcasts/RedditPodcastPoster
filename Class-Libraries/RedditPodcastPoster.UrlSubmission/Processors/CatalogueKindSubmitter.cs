using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Playables;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Languages;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;

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
    ICatalogueIdentityEnricher identityEnricher,
    IIdentityMetadataEnricher identityMetadata,
    IHeroEpisodePromoter heroEpisodePromoter,
    IFileKeyAllocator fileKeyAllocator,
    ICatalogueSubmitEnrichment enrichment,
    TimeProvider timeProvider,
    ILogger<CatalogueKindSubmitter> logger) : ICatalogueKindSubmitter
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
                RequiresCurator: classified.RequiresCurator,
                RejectionMessage: classified.RejectionMessage);
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
                return await EnrichExistingFilmServices(existing, source, submitOptions);
            }

            if (submitOptions.TargetFilmId is Guid targetFilmId)
            {
                return await AttachToFilm(targetFilmId, categorisedItem, submitOptions);
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
            MergePlatformServices(film, categorisedItem);
            if (filmAdmission.Admission.LanguageCode != null)
            {
                film.Language = filmAdmission.Admission.LanguageCode;
            }

            identityEnricher.ApplyMissing(film, filmIdentity.Admission.Ids);
            film.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.Film, film.Name, film.Id);
            var filmDetails = await enrichment.Enrich(film, categorisedItem);
            await FillFromIdentity(film);
            await films.Save(film);
            return CreatedPlayable(
                SubmitClassification.Film,
                film.Id,
                parentCreated: false,
                filmAdmission.Admission.SetsDefaultLanguage) with
            {
                FilmSearchSource = film,
                SubmitEpisodeDetails = filmDetails
            };
        }

        if (classified.ContentKind == SubmitClassification.TvShowEpisode)
        {
            var existing = await FindByCanonicalUrl(tvShowEpisodes, source);
            if (existing != null)
            {
                return await EnrichExistingTvEpisodeServices(existing, source, submitOptions);
            }

            var saved = await SaveTvShowEpisode(source, categorisedItem, submitOptions);
            if (saved.Stop != null)
            {
                return saved.Stop;
            }

            await PromoteIfEligible(saved.SearchSource, saved.Episode!.Id, submitOptions);
            return CreatedPlayable(
                SubmitClassification.TvShowEpisode,
                saved.Episode!.Id,
                saved.ParentCreated,
                saved.SetsDefaultLanguage,
                saved.SearchSource) with { SubmitEpisodeDetails = saved.Details };
        }

        if (classified.ContentKind == SubmitClassification.NewsReport)
        {
            var existing = await FindByCanonicalUrl(newsReports, source);
            if (existing != null)
            {
                return await EnrichExistingNewsReportServices(existing, source, submitOptions);
            }

            if (submitOptions.TargetNewsOrganisationId is Guid targetOrgId &&
                submitOptions.TargetNewsReportId is Guid targetReportId)
            {
                return await AttachToNewsReport(targetOrgId, targetReportId, categorisedItem, submitOptions);
            }

            var saved = await SaveNewsReport(source, categorisedItem, submitOptions);
            if (saved.Stop != null)
            {
                return saved.Stop;
            }

            await PromoteIfEligible(saved.SearchSource, saved.Report!.Id, submitOptions);
            return CreatedPlayable(
                SubmitClassification.NewsReport,
                saved.Report!.Id,
                saved.ParentCreated,
                saved.SetsDefaultLanguage,
                saved.SearchSource) with { SubmitEpisodeDetails = saved.Details };
        }

        return null;
    }

    /// <summary>
    /// A TV episode or news report this submit has just stored is appended to the homepage hero list when its parent
    /// (including a news organisation created by this submit, which is saved with always-promote-as-hero on) is
    /// flagged, with the same rule as the podcast submit and <see cref="CatalogueParentSubmitter"/>.
    /// </summary>
    private Task PromoteIfEligible(CatalogueSearchSource? searchSource, Guid playableId, SubmitOptions submitOptions)
    {
        if (!submitOptions.PersistToDatabase || searchSource is null)
        {
            return Task.CompletedTask;
        }

        return CatalogueHeroAutoPromotion.PromoteIfEligible(
            logger,
            heroEpisodePromoter,
            timeProvider,
            searchSource.Parent,
            searchSource.Episode,
            playableId,
            submitOptions.CreationSource);
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

    private async Task<(TvShowEpisode? Episode, bool ParentCreated, SubmitResult? Stop, bool SetsDefaultLanguage, CatalogueSearchSource? SearchSource, SubmitEpisodeDetails? Details)> SaveTvShowEpisode(
        ResolvedNonPodcastServiceItem source,
        CategorisedItem categorisedItem,
        SubmitOptions submitOptions)
    {
        var name = TvSeriesName(source, submitOptions);
        // #1587: Discovery+ episodes share a show UUID in the URL. Prefer that over name match so
        // a prior episode (even under a bad historical show title) still receives siblings.
        var parents = await FindTvShowsForEpisode(source, name);
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
            return (null, creatingParent, admitted.Stop, false, null, null);
        }

        var identity = await AdmitIdentityTv(show, RequiredTitle(source), submitOptions, SubmitClassification.TvShowEpisode);
        if (identity.Stop != null)
        {
            return (null, creatingParent, identity.Stop, false, null, null);
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
                null, null);
        }

        var saveParent = false;
        if (creatingParent)
        {
            if (admitted.Admission.LanguageCode != null)
            {
                show.Language = admitted.Admission.LanguageCode;
            }

            identityEnricher.ApplyMissing(show, identity.Admission.ParentIds ?? identity.Admission.Ids);
            show.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.TvShow, show.Name, show.Id);
            saveParent = true;
        }
        else if (identityEnricher.ApplyMissing(show, identity.Admission.ParentIds))
        {
            saveParent = true;
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
        // Enrich before anything is persisted (as the podcast create path does): the enrichers read Cosmos, and a
        // throw here must not leave a new parent saved with no child under it.
        var details = await enrichment.Enrich(episode, show, show.Indexing, categorisedItem);
        await FillFromIdentity(episode, show);
        if (saveParent)
        {
            await tvShows.Save(show);
        }

        await tvShowEpisodes.Save(episode);
        return (
            episode,
            creatingParent,
            null,
            admitted.Admission.SetsDefaultLanguage,
            ImmediateSearchSource(show, episode, CatalogueParentKind.TvShow),
            details);
    }

    /// <summary>
    /// RPP #1430: after the service enrichment, fills the new film's empty fields (description, release, runtime,
    /// image, alternative titles) from its TMDB identity. A failed lookup leaves the film as the service gave it.
    /// </summary>
    private async Task FillFromIdentity(Film film)
    {
        if ((film.TmdbId is null && string.IsNullOrWhiteSpace(film.ImdbId)) || !IdentityMetadataFill.HasGaps(film))
        {
            return;
        }

        var metadata = await identityMetadata.FetchFilmAsync(film.TmdbId, film.ImdbId);
        if (metadata is not null)
        {
            IdentityMetadataFill.Fill(film, metadata);
        }
    }

    /// <summary>RPP #1430: as <see cref="FillFromIdentity(Film)"/>, for a new TV episode (name, overview, air date, runtime, still).</summary>
    private async Task FillFromIdentity(TvShowEpisode episode, TvShow show)
    {
        if ((episode.TvdbId is not > 0 && string.IsNullOrWhiteSpace(episode.ImdbId)) ||
            !IdentityMetadataFill.HasGaps(episode))
        {
            return;
        }

        var metadata = await identityMetadata.FetchTvShowEpisodeAsync(episode, show.TmdbId);
        if (metadata is not null)
        {
            IdentityMetadataFill.Fill(episode, metadata);
        }
    }

    private async Task<(NewsReport? Report, bool ParentCreated, SubmitResult? Stop, bool SetsDefaultLanguage, CatalogueSearchSource? SearchSource, SubmitEpisodeDetails? Details)> SaveNewsReport(
        ResolvedNonPodcastServiceItem source,
        CategorisedItem categorisedItem,
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
            ? new NewsOrganisation(name)
            {
                PublisherName = TrimmedOrEmpty(source.Publisher),
                // A new news organisation is saved with bypass short / always promote as hero on.
                Indexing = CatalogueIndexingSource.ApplyNewsOrganisationCreationDefaults(null)
            }
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
            return (null, creatingParent, admitted.Stop, false, null, null);
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
                null, null);
        }

        if (creatingParent)
        {
            if (admitted.Admission.LanguageCode != null)
            {
                organisation.Language = admitted.Admission.LanguageCode;
            }

            organisation.FileKey = await fileKeyAllocator.Allocate(
                FileKeyKind.NewsOrganisation, organisation.Name, organisation.Id);
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

        // Enrich before anything is persisted; see SaveTvShowEpisode.
        var details = await enrichment.Enrich(report, organisation, organisation.Indexing, categorisedItem);
        if (creatingParent)
        {
            await newsOrganisations.Save(organisation);
        }

        await newsReports.Save(report);
        return (
            report,
            creatingParent,
            null,
            admitted.Admission.SetsDefaultLanguage,
            ImmediateSearchSource(organisation, report, CatalogueParentKind.NewsOrganisation),
            details);
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
            options.SelectedIdentityKey,
            length: source.Duration).ConfigureAwait(false);
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

    private async Task<IReadOnlyList<TvShow>> FindTvShowsForEpisode(
        ResolvedNonPodcastServiceItem source,
        string seriesName)
    {
        if (source.StreamingService == StreamingService.DiscoveryPlus &&
            source.Url is { } discoveryUrl &&
            DiscoveryPlusUrlMatcher.TryGetShowId(discoveryUrl, out var showId))
        {
            var byShowId = await FindTvShowsByDiscoveryPlusShowId(showId);
            if (byShowId.Count > 0)
            {
                return byShowId;
            }
        }

        return await PublisherNameAttachLookup.FindByName(tvShows, seriesName);
    }

    /// <summary>
    /// #1587: project Discovery+ service URLs for TV episodes, match the shared show UUID in memory
    /// (Cosmos LINQ cannot run string functions on Uri), then load distinct TvShows for matching episodes.
    /// Multiple distinct parents for one show UUID refuse attach (same as ambiguous name match).
    /// </summary>
    private async Task<IReadOnlyList<TvShow>> FindTvShowsByDiscoveryPlusShowId(string showId)
    {
        var serviceKey = StreamingServiceWire.ToKey(StreamingService.DiscoveryPlus);
        var byId = new Dictionary<Guid, TvShow>();
        await foreach (var stored in tvShowEpisodes.GetAllBy(
                           PlayableServiceUrlFilters.HasServiceLink<TvShowEpisode>(serviceKey),
                           PlayableServiceUrlFilters.ServiceUrl<TvShowEpisode>(serviceKey)))
        {
            if (stored is null || !DiscoveryPlusUrlMatcher.SameShow(stored, showId))
            {
                continue;
            }

            var match = stored;
            var episode = await tvShowEpisodes.GetBy(item =>
                item.Services != null && item.Services[serviceKey].Url == match);
            if (episode is null)
            {
                continue;
            }

            if (byId.ContainsKey(episode.TvShowId))
            {
                continue;
            }

            var show = await tvShows.GetBy(item => item.Id == episode.TvShowId);
            if (show != null)
            {
                byId[show.Id] = show;
            }
        }

        return byId.Values.ToArray();
    }

    private async Task<T?> FindByCanonicalUrl<T>(
        IFilterableRepository<T> repository,
        ResolvedNonPodcastServiceItem source)
        where T : class, IPlayable
    {
        if (source.Url is null || source.StreamingService is null)
        {
            return null;
        }

        var serviceKey = StreamingServiceWire.ToKey(source.StreamingService.Value);
        var canonical = CanonicalStoredUrl(source.Url);
        var exact = await FindByStoredServiceUrl(repository, serviceKey, canonical);
        if (exact != null)
        {
            return exact;
        }

        if (WbdCatalogueSiblingUrls.TryGet(
                source.StreamingService.Value, canonical, out var sibling, out var siblingUrl))
        {
            var bySibling = await FindByStoredServiceUrl(
                repository, StreamingServiceWire.ToKey(sibling), siblingUrl);
            if (bySibling != null)
            {
                return bySibling;
            }
        }

        if (source.StreamingService != StreamingService.Netflix)
        {
            return null;
        }

        return await FindByLegacyNetflixTitleUrl(repository, serviceKey, canonical);
    }

    /// <summary>
    /// Point lookup by exact stored service URL. Equality does not need a projected full-key scan;
    /// keep <see cref="FindByLegacyNetflixTitleUrl{T}"/> for compact-id matching that Cosmos LINQ cannot express.
    /// </summary>
    private static Task<T?> FindByStoredServiceUrl<T>(
        IFilterableRepository<T> repository,
        string serviceKey,
        Uri url)
        where T : class, IPlayable =>
        repository.GetBy(PlayableServiceUrlFilters.HoldsServiceUrl<T>(serviceKey, url, url));

    /// <summary>
    /// #1517: a Netflix title stored before canonicalisation may still carry a country segment
    /// (<c>/gb/title/{id}</c>), so the exact canonical match above misses it. Cosmos LINQ cannot run string
    /// functions on the stored <see cref="Uri"/>, so this projects only the stored Netflix URL of items that
    /// have a Netflix link (no full documents), compares compact title ids in memory, and then loads the
    /// match by its exact stored URL. It only runs after an exact miss for a Netflix <c>/title/</c> URL,
    /// i.e. just before a new item would be created.
    /// </summary>
    private static async Task<T?> FindByLegacyNetflixTitleUrl<T>(
        IFilterableRepository<T> repository,
        string serviceKey,
        Uri canonical)
        where T : class, IPlayable
    {
        var titleId = StreamingServiceCatalog.TryCompactUrl(serviceKey, canonical);
        if (titleId is null)
        {
            return null;
        }

        await foreach (var stored in repository.GetAllBy(
                           PlayableServiceUrlFilters.HasServiceLink<T>(serviceKey),
                           PlayableServiceUrlFilters.ServiceUrl<T>(serviceKey)))
        {
            if (stored is null || stored == canonical ||
                StreamingServiceCatalog.TryCompactUrl(serviceKey, stored) != titleId)
            {
                continue;
            }

            var match = stored;
            var found = await repository.GetBy(item =>
                item.Services != null && item.Services[serviceKey].Url == match);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private Dictionary<string, ServiceLink> ServiceMap(ResolvedNonPodcastServiceItem source)
    {
        if (source.Url is null || source.StreamingService is null)
        {
            return new Dictionary<string, ServiceLink>();
        }

        var canonical = CanonicalStoredUrl(source.Url);
        var link = new ServiceLink { Url = canonical };
        if (source.Image is not null)
        {
            link.Image = source.Image;
        }

        var map = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(source.StreamingService.Value)] = link
        };

        // Discovery+ / HBO Max share WBD catalogue UUIDs — store the sibling URL without copying the image.
        if (WbdCatalogueSiblingUrls.TryGet(
                source.StreamingService.Value, canonical, out var sibling, out var siblingUrl))
        {
            map[StreamingServiceWire.ToKey(sibling)] = new ServiceLink { Url = siblingUrl };
        }

        return map;
    }

    private async Task<SubmitResult> EnrichExistingFilmServices(
        Film existing,
        ResolvedNonPodcastServiceItem source,
        SubmitOptions submitOptions)
    {
        var map = ServiceMap(source);
        if (!submitOptions.PersistToDatabase)
        {
            var previewChanged = MergeMissingServiceLinks(existing, map);
            return AlreadyExists(SubmitClassification.Film, existing.Id) with
            {
                FilmSearchSource = existing,
                EpisodeResult = previewChanged ? SubmitResultState.Enriched : SubmitResultState.EpisodeAlreadyExists
            };
        }

        var changed = false;
        var updated = await films.Update(existing.Id, film =>
        {
            changed = MergeMissingServiceLinks(film, map);
            return changed;
        });
        if (updated is null)
        {
            return AlreadyExists(SubmitClassification.Film, existing.Id);
        }

        return AlreadyExists(SubmitClassification.Film, updated.Id) with
        {
            FilmSearchSource = updated,
            EpisodeResult = changed ? SubmitResultState.Enriched : SubmitResultState.EpisodeAlreadyExists
        };
    }

    private async Task<SubmitResult> EnrichExistingTvEpisodeServices(
        TvShowEpisode existing,
        ResolvedNonPodcastServiceItem source,
        SubmitOptions submitOptions)
    {
        var map = ServiceMap(source);
        if (!submitOptions.PersistToDatabase)
        {
            var previewChanged = MergeMissingServiceLinks(existing, map);
            CatalogueSearchSource? previewSearch = null;
            if (previewChanged)
            {
                var show = await tvShows.GetTvShow(existing.TvShowId) ??
                           throw new InvalidOperationException(
                               $"No TV show exists for id '{existing.TvShowId}'.");
                previewSearch = ImmediateSearchSource(show, existing, CatalogueParentKind.TvShow);
            }

            return AlreadyExists(SubmitClassification.TvShowEpisode, existing.Id) with
            {
                CatalogueSearchSource = previewSearch,
                EpisodeResult = previewChanged ? SubmitResultState.Enriched : SubmitResultState.EpisodeAlreadyExists
            };
        }

        var tagged = await tvShowEpisodes.GetTvShowEpisodeWithETag(existing.TvShowId, existing.Id);
        if (tagged is null)
        {
            return AlreadyExists(SubmitClassification.TvShowEpisode, existing.Id);
        }

        var changed = MergeMissingServiceLinks(tagged.Item, map);
        if (changed)
        {
            var replaced = await tvShowEpisodes.TryReplace(tagged.Item, tagged.ETag);
            if (!replaced)
            {
                tagged = await tvShowEpisodes.GetTvShowEpisodeWithETag(existing.TvShowId, existing.Id);
                if (tagged is null)
                {
                    return AlreadyExists(SubmitClassification.TvShowEpisode, existing.Id);
                }

                changed = MergeMissingServiceLinks(tagged.Item, map);
                if (changed)
                {
                    replaced = await tvShowEpisodes.TryReplace(tagged.Item, tagged.ETag);
                    if (!replaced)
                    {
                        throw new InvalidOperationException(
                            $"Could not save TV episode '{existing.Id}' because it changed while the sibling URL was being attached.");
                    }
                }
            }
        }

        CatalogueSearchSource? searchSource = null;
        if (changed)
        {
            var show = await tvShows.GetTvShow(tagged.Item.TvShowId) ??
                       throw new InvalidOperationException(
                           $"No TV show exists for id '{tagged.Item.TvShowId}'.");
            searchSource = ImmediateSearchSource(show, tagged.Item, CatalogueParentKind.TvShow);
        }

        return AlreadyExists(SubmitClassification.TvShowEpisode, tagged.Item.Id) with
        {
            CatalogueSearchSource = searchSource,
            EpisodeResult = changed ? SubmitResultState.Enriched : SubmitResultState.EpisodeAlreadyExists
        };
    }

    private async Task<SubmitResult> EnrichExistingNewsReportServices(
        NewsReport existing,
        ResolvedNonPodcastServiceItem source,
        SubmitOptions submitOptions)
    {
        var map = ServiceMap(source);
        if (!submitOptions.PersistToDatabase)
        {
            var previewChanged = MergeMissingServiceLinks(existing, map);
            CatalogueSearchSource? previewSearch = null;
            if (previewChanged)
            {
                var organisation = await newsOrganisations.GetNewsOrganisation(existing.NewsOrganisationId) ??
                                   throw new InvalidOperationException(
                                       $"No news organisation exists for id '{existing.NewsOrganisationId}'.");
                previewSearch = ImmediateSearchSource(
                    organisation, existing, CatalogueParentKind.NewsOrganisation);
            }

            return AlreadyExists(SubmitClassification.NewsReport, existing.Id) with
            {
                CatalogueSearchSource = previewSearch,
                EpisodeResult = previewChanged ? SubmitResultState.Enriched : SubmitResultState.EpisodeAlreadyExists
            };
        }

        var tagged = await newsReports.GetNewsReportWithETag(existing.NewsOrganisationId, existing.Id);
        if (tagged is null)
        {
            return AlreadyExists(SubmitClassification.NewsReport, existing.Id);
        }

        var changed = MergeMissingServiceLinks(tagged.Item, map);
        if (changed)
        {
            var replaced = await newsReports.TryReplace(tagged.Item, tagged.ETag);
            if (!replaced)
            {
                tagged = await newsReports.GetNewsReportWithETag(existing.NewsOrganisationId, existing.Id);
                if (tagged is null)
                {
                    return AlreadyExists(SubmitClassification.NewsReport, existing.Id);
                }

                changed = MergeMissingServiceLinks(tagged.Item, map);
                if (changed)
                {
                    replaced = await newsReports.TryReplace(tagged.Item, tagged.ETag);
                    if (!replaced)
                    {
                        throw new InvalidOperationException(
                            $"Could not save news report '{existing.Id}' because it changed while the sibling URL was being attached.");
                    }
                }
            }
        }

        CatalogueSearchSource? searchSource = null;
        if (changed)
        {
            var organisation = await newsOrganisations.GetNewsOrganisation(tagged.Item.NewsOrganisationId) ??
                               throw new InvalidOperationException(
                                   $"No news organisation exists for id '{tagged.Item.NewsOrganisationId}'.");
            searchSource = ImmediateSearchSource(
                organisation, tagged.Item, CatalogueParentKind.NewsOrganisation);
        }

        return AlreadyExists(SubmitClassification.NewsReport, tagged.Item.Id) with
        {
            CatalogueSearchSource = searchSource,
            EpisodeResult = changed ? SubmitResultState.Enriched : SubmitResultState.EpisodeAlreadyExists
        };
    }

    /// <summary>
    /// Fills missing service URLs (and images only when the target has none) from a submit ServiceMap.
    /// Returns true when the playable's services changed.
    /// </summary>
    private static bool MergeMissingServiceLinks(
        IPlayable playable,
        IReadOnlyDictionary<string, ServiceLink> map)
    {
        playable.Services ??= new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var changed = false;
        foreach (var (key, link) in map)
        {
            if (link?.Url is null)
            {
                continue;
            }

            if (!playable.Services.TryGetValue(key, out var existing) || existing?.Url is null)
            {
                playable.Services[key] = new ServiceLink { Url = link.Url, Image = link.Image };
                changed = true;
                continue;
            }

            if (existing.Image is null && link.Image is not null)
            {
                existing.Image = link.Image;
                changed = true;
            }
        }

        return changed;
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

    private async Task<SubmitResult> AttachToFilm(
        Guid filmId,
        CategorisedItem categorisedItem,
        SubmitOptions submitOptions)
    {
        var version = await films.GetFilmForUpdate(filmId);
        if (version == null)
        {
            throw new InvalidOperationException($"No film exists for id '{filmId}'.");
        }

        var film = version.Film;
        MergePlatformServices(film, categorisedItem);
        if (submitOptions.LanguageProbe)
        {
            return LanguageProbeResult(SubmitClassification.Film, false);
        }

        var details = await enrichment.Enrich(film, categorisedItem);
        if (submitOptions.PersistToDatabase)
        {
            var replaced = await films.TryReplace(film, version.ETag);
            if (!replaced)
            {
                version = await films.GetFilmForUpdate(filmId) ??
                          throw new InvalidOperationException($"No film exists for id '{filmId}'.");
                film = version.Film;
                MergePlatformServices(film, categorisedItem);
                details = await enrichment.Enrich(film, categorisedItem);
                replaced = await films.TryReplace(film, version.ETag);
                if (!replaced)
                {
                    throw new InvalidOperationException(
                        $"Could not save film '{filmId}' because it changed while the URL was being attached.");
                }
            }
        }

        return AlreadyExists(SubmitClassification.Film, film.Id) with
        {
            FilmSearchSource = film,
            SubmitEpisodeDetails = details,
            EpisodeResult = SubmitResultState.Enriched
        };
    }

    private async Task<SubmitResult> AttachToNewsReport(
        Guid organisationId,
        Guid reportId,
        CategorisedItem categorisedItem,
        SubmitOptions submitOptions)
    {
        var version = await newsReports.GetNewsReportWithETag(organisationId, reportId);
        if (version == null)
        {
            throw new InvalidOperationException(
                $"No news report '{reportId}' exists under organisation '{organisationId}'.");
        }

        var report = version.Item;
        var organisation = await newsOrganisations.GetNewsOrganisation(organisationId) ??
                           throw new InvalidOperationException($"No news organisation exists for id '{organisationId}'.");
        MergePlatformServices(report, categorisedItem);
        if (submitOptions.LanguageProbe)
        {
            return LanguageProbeResult(SubmitClassification.NewsReport, false);
        }

        var details = await enrichment.Enrich(report, organisation, organisation.Indexing, categorisedItem);
        if (submitOptions.PersistToDatabase)
        {
            var replaced = await newsReports.TryReplace(report, version.ETag);
            if (!replaced)
            {
                version = await newsReports.GetNewsReportWithETag(organisationId, reportId) ??
                          throw new InvalidOperationException(
                              $"No news report '{reportId}' exists under organisation '{organisationId}'.");
                report = version.Item;
                MergePlatformServices(report, categorisedItem);
                details = await enrichment.Enrich(report, organisation, organisation.Indexing, categorisedItem);
                replaced = await newsReports.TryReplace(report, version.ETag);
                if (!replaced)
                {
                    throw new InvalidOperationException(
                        $"Could not save news report '{reportId}' because it changed while the URL was being attached.");
                }
            }
        }

        return AlreadyExists(SubmitClassification.NewsReport, report.Id) with
        {
            CatalogueSearchSource = ImmediateSearchSource(
                organisation, report, CatalogueParentKind.NewsOrganisation),
            SubmitEpisodeDetails = details,
            EpisodeResult = SubmitResultState.Enriched
        };
    }

    private static void MergePlatformServices(ICatalogueYouTubeItem item, CategorisedItem categorisedItem)
    {
        var services = item.Services ?? new Dictionary<string, ServiceLink>();
        AddService(services, ServiceKeys.YouTube, categorisedItem.ResolvedYouTubeItem?.Url,
            categorisedItem.ResolvedYouTubeItem?.Image);
        AddService(services, ServiceKeys.Spotify, categorisedItem.ResolvedSpotifyItem?.Url,
            categorisedItem.ResolvedSpotifyItem?.Image);
        AddService(services, ServiceKeys.Apple, categorisedItem.ResolvedAppleItem?.Url,
            categorisedItem.ResolvedAppleItem?.Image);
        item.Services = services;
        if (string.IsNullOrWhiteSpace(item.YouTubeId) &&
            item.Services.TryGetValue(ServiceKeys.YouTube, out var youtube) && youtube?.Url != null)
        {
            item.YouTubeId = YouTubeIdResolver.Extract(youtube.Url);
        }
    }

    private static void AddService(
        Dictionary<string, ServiceLink> services,
        string key,
        Uri? url,
        Uri? image)
    {
        if (url == null)
        {
            return;
        }

        if (!services.TryGetValue(key, out var link) || link is null)
        {
            link = new ServiceLink();
            services[key] = link;
        }

        link.Url ??= url;
        link.Image ??= image;
    }
}