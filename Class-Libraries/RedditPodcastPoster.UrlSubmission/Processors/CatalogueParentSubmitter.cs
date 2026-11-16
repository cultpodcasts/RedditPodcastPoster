using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Languages;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public interface ICatalogueParentSubmitter
{
    Task<SubmitResult> Submit(
        CatalogueParentMatch parent,
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions);
}

/// <summary>
/// Stores a URL from a channel / show owned by a news organisation or TV show as a NewsReport /
/// TvShowEpisode, never as a podcast episode. The parent runs through the podcast pipeline as the same
/// transient podcast the indexer uses (<see cref="CatalogueIndexingSource.ToTransientPodcast"/>), so
/// categorise, cross-service matching, subjects and guests behave as for a podcast submit; the episode
/// is then mapped with <see cref="CatalogueParentKindMapper"/> (as kind transfer and the indexer do).
/// The result carries a <see cref="CatalogueSearchSource"/> so the host indexes it immediately.
/// A created playable under a parent flagged <see cref="CatalogueIndexingSource.AlwaysPromoteAsHero"/> is appended to
/// the homepage hero list, with the same <see cref="HeroAutoPromoteSelector"/> rule as a podcast submit.
/// </summary>
public class CatalogueParentSubmitter(
    IUrlCategoriser urlCategoriser,
    IPodcastProcessor podcastProcessor,
    INewsReportRepository newsReports,
    ITvShowEpisodeRepository tvShowEpisodes,
    ICatalogueLanguageGate languageGate,
    IHeroEpisodePromoter heroEpisodePromoter,
    TimeProvider timeProvider,
    ILogger<CatalogueParentSubmitter> logger) : ICatalogueParentSubmitter
{
    public async Task<SubmitResult> Submit(
        CatalogueParentMatch parent,
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var transient = (parent.Indexing ?? new CatalogueIndexingSource()).ToTransientPodcast(parent.Parent);

        // The parent's stored playables are its "episodes": the categoriser and processor match the URL against them
        // (platform url / id, then IsMatchingEpisode) exactly as for a podcast, so a resubmit, a cross-platform
        // submit or another URL form of the same video finds the existing item instead of creating a duplicate.
        var stored = await LoadPlayables(parent);
        var known = stored.Select(playable => CatalogueParentKnownEpisodes.ToEpisode(playable, transient)).ToList();
        var categorisedItem = await urlCategoriser.Categorise(
            transient,
            known,
            url,
            indexingContext,
            submitOptions.MatchOtherServices,
            submitOptions.PrefetchedMeta,
            submitOptions.RefreshMeta);

        if (submitOptions.LanguageChoice == CatalogueLanguageChoice.Cancel)
        {
            return new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: parent.ContentKind,
                LanguageHold: new CatalogueLanguageHold(string.Empty, string.Empty, []));
        }

        if (submitOptions.EnforceLanguageGate || submitOptions.LanguageProbe)
        {
            var parentNoun = parent.Kind == CatalogueParentKind.NewsOrganisation ? "News organisation" : "TV show";
            var childNoun = parent.Kind == CatalogueParentKind.NewsOrganisation ? "Report" : "Episode";
            // Admit from categorised authority texts before create/enrich (same order as CategorisedItemProcessor).
            var podcastShaped = CatalogueLanguageTextReader.FromCategorisedItem(
                categorisedItem,
                nameOverride: parent.Parent.Name,
                existingParent: parent.Parent);
            string? Field(string key) =>
                podcastShaped.Fields.FirstOrDefault(f => f.Key == key)?.Text;
            var admission = await languageGate.Evaluate(
                CatalogueLanguageTextReader.ForParentAndChild(
                    Field("parentTitle"),
                    Field("parentDescription"),
                    Field("episodeTitle"),
                    Field("episodeDescription"),
                    parentNoun,
                    childNoun),
                creatingNewParent: false,
                submitOptions);
            if (admission.Hold)
            {
                return new SubmitResult(
                    SubmitResultState.None,
                    SubmitResultState.None,
                    ContentKind: parent.ContentKind,
                    LanguageHold: admission.Prompt);
            }

            if (submitOptions.LanguageProbe)
            {
                return new SubmitResult(
                    SubmitResultState.None,
                    SubmitResultState.None,
                    ContentKind: parent.ContentKind,
                    DefaultLanguageFromDetection: admission.SetsDefaultLanguage);
            }
        }

        if (submitOptions.TargetNewsReportId is Guid targetReportId &&
            parent.Kind == CatalogueParentKind.NewsOrganisation)
        {
            return await AttachToTargetedNewsReport(
                parent, transient, categorisedItem, targetReportId, submitOptions);
        }

        var processed = await podcastProcessor.AddEpisodeToExistingPodcast(categorisedItem);
        if (processed.Episode is { } matched && stored.FirstOrDefault(x => x.Id == matched.Id) is { } existing)
        {
            return await UpdateExisting(parent, transient, url, processed, matched, existing, submitOptions);
        }

        if (processed is not { EpisodeResult: SubmitResultState.Created, Episode: { } episode })
        {
            logger.LogWarning(
                "Submit of '{Url}' to {Kind} '{ParentName}' ({ParentId}) produced no new item (episode state '{EpisodeResult}').",
                url, parent.Kind, parent.Parent.Name, parent.Parent.Id, processed.EpisodeResult);
            return new SubmitResult(
                processed.EpisodeResult,
                SubmitResultState.None,
                ContentKind: parent.ContentKind);
        }

        // Belt and braces: an exact service url or platform id already stored under the parent.
        var existingId = FindExisting(stored, episode);
        if (existingId != null)
        {
            logger.LogInformation(
                "Submit of '{Url}': {Kind} '{ParentName}' already has playable '{PlayableId}' with this url.",
                url, parent.Kind, parent.Parent.Name, existingId);
            return new SubmitResult(
                SubmitResultState.EpisodeAlreadyExists,
                SubmitResultState.None,
                ContentKind: parent.ContentKind,
                PlayableId: existingId);
        }

        if (submitOptions.EnrichOnly)
        {
            logger.LogWarning(
                "Submit of '{Url}' to {Kind} '{ParentName}' ({ParentId}) matched no stored item; enrich-only, so nothing is created.",
                url, parent.Kind, parent.Parent.Name, parent.Parent.Id);
            return new SubmitResult(
                SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: parent.ContentKind);
        }

        Playable playable = parent.Parent switch
        {
            NewsOrganisation organisation => CatalogueParentKindMapper.ToNewsReport(episode, organisation),
            TvShow show => CatalogueParentKindMapper.ToTvShowEpisode(episode, show),
            _ => throw new InvalidOperationException($"Unsupported catalogue parent '{parent.Parent.GetType().Name}'.")
        };

        if (!submitOptions.PersistToDatabase)
        {
            logger.LogWarning("Bypassing persisting {ContentKind}.", parent.ContentKind);
            return new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                processed.SubmitEpisodeDetails,
                ContentKind: parent.ContentKind,
                PlayableId: playable.Id);
        }

        await Save(playable);
        logger.LogInformation(
            "Submit of '{Url}' stored {ContentKind} '{PlayableId}' under {Kind} '{ParentName}' ({ParentId}); no podcast created.",
            url, parent.ContentKind, playable.Id, parent.Kind, parent.Parent.Name, parent.Parent.Id);
        await PromoteIfEligible(transient, episode, playable.Id, submitOptions);
        return new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            processed.SubmitEpisodeDetails,
            ContentKind: parent.ContentKind,
            PlayableId: playable.Id,
            CatalogueSearchSource: new CatalogueSearchSource(transient, episode, parent.Kind));
    }

    /// <summary>
    /// The URL matched a stored playable. Enriched (a new service, subjects or guests): copy the merged episode onto
    /// the playable, save and re-index. Otherwise it already exists and nothing is written.
    /// </summary>
    private async Task<SubmitResult> UpdateExisting(
        CatalogueParentMatch parent,
        Podcast transient,
        Uri url,
        SubmitResult processed,
        Episode merged,
        Playable existing,
        SubmitOptions submitOptions)
    {
        if (processed.EpisodeResult != SubmitResultState.Enriched)
        {
            logger.LogInformation(
                "Submit of '{Url}': {Kind} '{ParentName}' already has playable '{PlayableId}'.",
                url, parent.Kind, parent.Parent.Name, existing.Id);
            return new SubmitResult(
                SubmitResultState.EpisodeAlreadyExists,
                SubmitResultState.None,
                processed.SubmitEpisodeDetails,
                ContentKind: parent.ContentKind,
                PlayableId: existing.Id);
        }

        ApplyEnrichment(merged, existing);
        if (!submitOptions.PersistToDatabase)
        {
            logger.LogWarning("Bypassing persisting enriched {ContentKind} '{PlayableId}'.", parent.ContentKind,
                existing.Id);
            return new SubmitResult(
                SubmitResultState.Enriched,
                SubmitResultState.None,
                processed.SubmitEpisodeDetails,
                ContentKind: parent.ContentKind,
                PlayableId: existing.Id);
        }

        await Save(existing);
        logger.LogInformation(
            "Submit of '{Url}' enriched {ContentKind} '{PlayableId}' under {Kind} '{ParentName}' ({ParentId}).",
            url, parent.ContentKind, existing.Id, parent.Kind, parent.Parent.Name, parent.Parent.Id);
        return new SubmitResult(
            SubmitResultState.Enriched,
            SubmitResultState.None,
            processed.SubmitEpisodeDetails,
            ContentKind: parent.ContentKind,
            PlayableId: existing.Id,
            CatalogueSearchSource: new CatalogueSearchSource(transient, merged, parent.Kind));
    }

    /// <summary>Same rule as <c>CategorisedItemProcessor</c> for a podcast: flag on, not ignored / removed, in the week window.</summary>
    private Task PromoteIfEligible(Podcast transient, Episode episode, Guid playableId, SubmitOptions submitOptions) =>
        CatalogueHeroAutoPromotion.PromoteIfEligible(
            logger, heroEpisodePromoter, timeProvider, transient, episode, playableId, submitOptions.CreationSource);

    /// <summary>What the podcast enricher may change on a matched episode, copied back onto the stored playable.</summary>
    private static void ApplyEnrichment(Episode merged, Playable target)
    {
        target.Title = merged.Title;
        target.Description = merged.Description;
        target.Length = merged.Length;
        target.Explicit = merged.Explicit;
        target.SetRelease(merged.Release);
        target.Services = merged.Services?.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language });
        if (target is ICatalogueYouTubeItem catalogueYouTube)
        {
            // Keep the compact id the search pull reads (c.youTubeId) in step with services.youtube.url.
            // Url wins; a missing or unparseable url never clears an existing id.
            EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(catalogueYouTube);
        }
        target.Subjects = [.. merged.Subjects];
        target.Guests = merged.Guests is null ? null : [.. merged.Guests];
    }

    private async Task<SubmitResult> AttachToTargetedNewsReport(
        CatalogueParentMatch parent,
        Podcast transient,
        CategorisedItem categorisedItem,
        Guid reportId,
        SubmitOptions submitOptions)
    {
        var organisationId = parent.Parent.Id;
        var version = await newsReports.GetNewsReportWithETag(organisationId, reportId);
        if (version == null)
        {
            throw new InvalidOperationException(
                $"No news report '{reportId}' exists under organisation '{organisationId}'.");
        }

        var report = version.Item;
        MergePlatformServices(report, categorisedItem);
        if (!submitOptions.PersistToDatabase)
        {
            return new SubmitResult(
                SubmitResultState.Enriched,
                SubmitResultState.None,
                ContentKind: parent.ContentKind,
                PlayableId: report.Id);
        }

        var replaced = await newsReports.TryReplace(report, version.ETag);
        if (!replaced)
        {
            version = await newsReports.GetNewsReportWithETag(organisationId, reportId) ??
                      throw new InvalidOperationException(
                          $"No news report '{reportId}' exists under organisation '{organisationId}'.");
            report = version.Item;
            MergePlatformServices(report, categorisedItem);
            replaced = await newsReports.TryReplace(report, version.ETag);
            if (!replaced)
            {
                throw new InvalidOperationException(
                    $"Could not save news report '{reportId}' because it changed while the URL was being attached.");
            }
        }

        logger.LogInformation(
            "Submit of a discovery URL attached to news report '{PlayableId}' under '{ParentName}' ({ParentId}).",
            report.Id, parent.Parent.Name, parent.Parent.Id);
        return new SubmitResult(
            SubmitResultState.Enriched,
            SubmitResultState.None,
            ContentKind: parent.ContentKind,
            PlayableId: report.Id,
            CatalogueSearchSource: new CatalogueSearchSource(transient, CatalogueParentKnownEpisodes.ToEpisode(report, transient), parent.Kind));
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
            item.Services.TryGetValue(ServiceKeys.YouTube, out var youtube) && youtube.Url != null)
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

        if (!services.TryGetValue(key, out var link))
        {
            link = new ServiceLink();
            services[key] = link;
        }

        link.Url ??= url;
        link.Image ??= image;
    }

    private async Task<IReadOnlyList<Playable>> LoadPlayables(CatalogueParentMatch parent)
    {
        var playables = new List<Playable>();
        if (parent.Kind == CatalogueParentKind.NewsOrganisation)
        {
            await foreach (var report in newsReports.GetByNewsOrganisationId(parent.Parent.Id))
            {
                playables.Add(report);
            }
        }
        else
        {
            await foreach (var episode in tvShowEpisodes.GetByTvShowId(parent.Parent.Id))
            {
                playables.Add(episode);
            }
        }

        return playables;
    }

    private async Task Save(Playable playable)
    {
        switch (playable)
        {
            case NewsReport report:
                await newsReports.Save(report);
                break;
            case TvShowEpisode tvShowEpisode:
                await tvShowEpisodes.Save(tvShowEpisode);
                break;
        }
    }

    /// <summary>A stored playable that already has one of the new item's service urls or platform ids.</summary>
    private static Guid? FindExisting(IReadOnlyList<Playable> stored, Episode episode)
    {
        var ids = CatalogueParentKnownEpisodes.IdsFromServices(episode.Services);
        foreach (var playable in stored)
        {
            if (episode.Services is { Count: > 0 } services && playable.Services is { } existingServices &&
                services.Any(pair => pair.Value?.Url is { } u &&
                                     existingServices.TryGetValue(pair.Key, out var link) && link.Url == u))
            {
                return playable.Id;
            }

            if (ids != null && CatalogueParentKnownEpisodes.IdsFromServices(playable.Services) is { } storedIds &&
                ((ids.YouTube != null && ids.YouTube == storedIds.YouTube) ||
                 (ids.Spotify != null && ids.Spotify == storedIds.Spotify) ||
                 (ids.Apple != null && ids.Apple == storedIds.Apple)))
            {
                return playable.Id;
            }
        }

        return null;
    }
}
