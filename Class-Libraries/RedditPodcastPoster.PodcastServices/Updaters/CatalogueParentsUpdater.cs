using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Indexes TvShow and NewsOrganisation parents that kept auto-index discovery settings
/// (<see cref="CatalogueIndexingSource"/>) from their migrated podcast. Each parent becomes a
/// transient <see cref="Podcast"/>; its playables in the release window are projected to
/// <see cref="Episode"/> (provider ids derived from <c>services</c> URLs, since playables do
/// not store them). Discovery, merge, the minimum-duration rule and elimination terms are the
/// same as <see cref="PodcastUpdater"/>. New items are saved as TvShowEpisode / NewsReport;
/// merged and term-removed items update the stored playable. Cross-platform enrichment and
/// hero promotion are not run for these kinds.
/// </summary>
public class CatalogueParentsUpdater(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IEpisodeProvider episodeProvider,
    IEpisodeMerger episodeMerger,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    IOptions<PostingCriteria> postingCriteria,
    IPodcastPassApiCache passApiCache,
    TimeProvider timeProvider,
    ILogger<CatalogueParentsUpdater> logger) : ICatalogueParentsUpdater
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<Guid[]> GetIndexableParentIds()
    {
        var tvShows = await tvShowRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           x.Indexing != null &&
                           (x.Indexing.IndexAllEpisodes || x.Indexing.EpisodeIncludeTitleRegex != ""))
            .ToListAsync();
        var organisations = await newsOrganisationRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           x.Indexing != null &&
                           (x.Indexing.IndexAllEpisodes || x.Indexing.EpisodeIncludeTitleRegex != ""))
            .ToListAsync();

        return tvShows.Where(IsIndexable).Select(x => x.Id)
            .Concat(organisations.Where(IsIndexable).Select(x => x.Id))
            .Distinct()
            .Order()
            .ToArray();
    }

    public async Task<bool> UpdateParents(Guid[] parentIds, IndexingContext indexingContext)
    {
        ArgumentNullException.ThrowIfNull(parentIds);
        if (!indexingContext.ReleasedSince.HasValue)
        {
            throw new InvalidOperationException("Cannot index catalogue parents without a released-since.");
        }

        var success = true;
        foreach (var parentId in parentIds)
        {
            try
            {
                var tvShow = await tvShowRepository.GetTvShow(parentId);
                if (tvShow is not null)
                {
                    if (IsIndexable(tvShow))
                    {
                        await IndexTvShow(tvShow, indexingContext.ForPodcastUpdate());
                    }

                    continue;
                }

                var organisation = await newsOrganisationRepository.GetNewsOrganisation(parentId);
                if (organisation is not null && IsIndexable(organisation))
                {
                    await IndexNewsOrganisation(organisation, indexingContext.ForPodcastUpdate());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure indexing catalogue parent with id '{ParentId}'.", parentId);
                success = false;
            }
            finally
            {
                passApiCache.Clear();
            }
        }

        return success;
    }

    private static bool IsIndexable(Publisher parent) =>
        !parent.IsRemoved() && IndexingOf(parent) is { IsAutoIndexed: true };

    private static CatalogueIndexingSource? IndexingOf(Publisher parent) => parent switch
    {
        TvShow show => show.Indexing,
        NewsOrganisation organisation => organisation.Indexing,
        _ => null
    };

    private async Task IndexTvShow(TvShow show, IndexingContext context)
    {
        var outcome = await IndexParent(
            show,
            show.Indexing!,
            since => tvShowEpisodeRepository
                .GetByTvShowId(show.Id, PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since))
                .ToListAsync().AsTask(),
            episode => CatalogueParentKindMapper.ToTvShowEpisode(episode, show),
            context);
        if (outcome.ChangedPlayables.Count > 0)
        {
            await tvShowEpisodeRepository.Save(outcome.ChangedPlayables);
        }

        if (outcome.ParentChanged)
        {
            await tvShowRepository.Save(show);
        }

        LogOutcome("TvShow", show, outcome);
    }

    private async Task IndexNewsOrganisation(NewsOrganisation organisation, IndexingContext context)
    {
        var outcome = await IndexParent(
            organisation,
            organisation.Indexing!,
            since => newsReportRepository
                .GetByNewsOrganisationId(organisation.Id, PlayableCosmosFilters.ReleasedOnOrAfter<NewsReport>(since))
                .ToListAsync().AsTask(),
            episode => CatalogueParentKindMapper.ToNewsReport(episode, organisation),
            context);
        if (outcome.ChangedPlayables.Count > 0)
        {
            await newsReportRepository.Save(outcome.ChangedPlayables);
        }

        if (outcome.ParentChanged)
        {
            await newsOrganisationRepository.Save(organisation);
        }

        LogOutcome("NewsOrganisation", organisation, outcome);
    }

    private void LogOutcome<TPlayable>(string kind, Publisher parent, ParentIndexOutcome<TPlayable> outcome)
        where TPlayable : Playable
    {
        logger.LogInformation(
            "Catalogue parent indexed kind='{Kind}' id='{ParentId}' name='{ParentName}' added='{Added}' merged='{Merged}' removed-by-terms='{Filtered}' succeeded='{Succeeded}'.",
            kind, parent.Id, parent.Name, outcome.Added, outcome.Merged, outcome.Filtered, outcome.Succeeded);
    }

    private async Task<ParentIndexOutcome<TPlayable>> IndexParent<TPlayable>(
        Publisher parent,
        CatalogueIndexingSource source,
        Func<DateTime, Task<List<TPlayable>>> loadPlayables,
        Func<Episode, TPlayable> createPlayable,
        IndexingContext indexingContext)
        where TPlayable : Playable
    {
        var podcast = source.ToTransientPodcast(parent);
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        var releasedSince = indexingContext.ReleasedSince!.Value;
        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            releasedSince += youTubePublishingDelay;
        }

        var repositoryReleasedSince = youTubePublishingDelay < TimeSpan.Zero
            ? releasedSince
            : releasedSince - youTubePublishingDelay;

        var stored = await loadPlayables(repositoryReleasedSince);
        var storedById = stored.ToDictionary(x => x.Id);
        var existing = stored.Select(x => ToEpisode(x, podcast)).ToList();

        var initialSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
        var initialSkipYouTube = indexingContext.SkipYouTubeUrlResolving;
        var newEpisodes = await episodeProvider.GetEpisodes(podcast, existing, indexingContext);
        ApplyMinimumDuration(podcast, newEpisodes, indexingContext);

        var merge = episodeMerger.MergeEpisodes(podcast, existing, newEpisodes);
        if (podcast.HasIgnoreAllEpisodes())
        {
            foreach (var added in merge.AddedEpisodes)
            {
                added.Ignored = true;
            }
        }

        var inScope = existing.Concat(merge.AddedEpisodes).ToList();
        var eliminationTerms = (await eliminationTermsProviderInstance.GetAsync()).GetEliminationTerms();
        var filtered = podcastFilter.Filter(podcast, inScope, eliminationTerms.Terms);

        var changed = new Dictionary<Guid, TPlayable>();
        foreach (var (merged, _) in merge.MergedEpisodes)
        {
            if (storedById.TryGetValue(merged.Id, out var target))
            {
                ApplyMerged(merged, target);
                changed[target.Id] = target;
            }
        }

        foreach (var filteredEpisode in filtered.FilteredEpisodes)
        {
            if (storedById.TryGetValue(filteredEpisode.Episode.Id, out var target))
            {
                target.Removed = true;
                changed[target.Id] = target;
            }
        }

        foreach (var added in merge.AddedEpisodes)
        {
            changed[added.Id] = createPlayable(added);
        }

        source.CaptureFrom(podcast);
        var bypassed = initialSkipSpotify != indexingContext.SkipSpotifyUrlResolving ||
                       initialSkipYouTube != indexingContext.SkipYouTubeUrlResolving ||
                       podcast.IsScheduledYouTubeDiscoveryBypassed(indexingContext);
        var succeeded = !bypassed && !merge.FailedEpisodes.Any();
        if (succeeded)
        {
            parent.LastIndexed = timeProvider.GetUtcNow().UtcDateTime;
        }

        var newest = merge.AddedEpisodes.Select(x => (DateTime?)x.ReleaseUtc)
            .Concat(merge.MergedEpisodes.Select(x => (DateTime?)x.Existing.ReleaseUtc))
            .Max();
        if (newest.HasValue && (parent.LatestReleased == null || newest > parent.LatestReleased))
        {
            parent.LatestReleased = newest;
        }

        // Discovery flags (expensive queries, show counts) live on Indexing, so save the parent every pass.
        return new ParentIndexOutcome<TPlayable>(
            [.. changed.Values],
            ParentChanged: true,
            merge.AddedEpisodes.Count,
            merge.MergedEpisodes.Count,
            filtered.FilteredEpisodes.Count,
            succeeded);
    }

    private void ApplyMinimumDuration(Podcast podcast, IList<Episode> newEpisodes, IndexingContext indexingContext)
    {
        var bypassGlobalShortCheck = podcast.BypassShortEpisodeChecking == true;
        var enforceMinimumDuration = podcast.MinimumDuration.HasValue || !bypassGlobalShortCheck;
        if (!enforceMinimumDuration)
        {
            return;
        }

        var threshold = podcast.MinimumDuration ?? _postingCriteria.MinimumDuration;
        foreach (var newEpisode in newEpisodes)
        {
            newEpisode.Ignored = newEpisode.Length < threshold;
        }

        if (indexingContext.SkipShortEpisodes || podcast.MinimumDuration.HasValue)
        {
            foreach (var shortEpisode in newEpisodes.Where(x => x.Ignored).ToList())
            {
                newEpisodes.Remove(shortEpisode);
            }
        }
    }

    /// <summary>Episode view of a stored playable, for the podcast merger and filters.</summary>
    internal static Episode ToEpisode(Playable playable, Podcast podcast)
    {
        var episode = new Episode
        {
            Id = playable.Id,
            PodcastId = podcast.Id,
            PodcastName = podcast.Name,
            Title = playable.Title,
            Description = playable.Description,
            Length = playable.Length,
            Explicit = playable.Explicit,
            Posted = playable.Posted,
            Tweeted = playable.Tweeted,
            Ignored = playable.Ignored,
            Removed = playable.Removed,
            Language = playable.Language,
            Subjects = [.. playable.Subjects],
            RemovedSubjects = [.. playable.RemovedSubjects],
            SearchTerms = playable.SearchTerms,
            HashTag = playable.HashTag,
            Services = playable.Services?.ToDictionary(
                pair => pair.Key,
                pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language })
        };
        episode.SetRelease(playable.Release);
        episode.Ids = IdsFromServices(playable.Services);
        return episode;
    }

    /// <summary>Spotify / Apple / YouTube ids parsed from <c>services</c> URLs (playables store no provider ids).</summary>
    internal static EpisodeIds? IdsFromServices(IReadOnlyDictionary<string, ServiceLink>? services)
    {
        if (services is null || services.Count == 0)
        {
            return null;
        }

        var ids = new EpisodeIds();
        if (services.TryGetValue(ServiceKeys.Spotify, out var spotify) && spotify.Url is not null)
        {
            var spotifyId = SpotifyIdResolver.GetEpisodeId(spotify.Url);
            ids.Spotify = string.IsNullOrWhiteSpace(spotifyId) ? null : spotifyId;
        }

        if (services.TryGetValue(ServiceKeys.Apple, out var apple) && apple.Url is not null)
        {
            ids.Apple = AppleIdResolver.GetEpisodeId(apple.Url);
        }

        if (services.TryGetValue(ServiceKeys.YouTube, out var youTube) && youTube.Url is not null)
        {
            ids.YouTube = YouTubeIdResolver.Extract(youTube.Url);
        }

        return ids.IsEmpty ? null : ids;
    }

    private static void ApplyMerged(Episode merged, Playable target)
    {
        target.Title = merged.Title;
        target.Description = merged.Description;
        target.Length = merged.Length;
        target.Explicit = merged.Explicit;
        target.SetRelease(merged.Release);
        target.Services = merged.Services?.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language });
    }

    private sealed record ParentIndexOutcome<TPlayable>(
        IReadOnlyList<TPlayable> ChangedPlayables,
        bool ParentChanged,
        int Added,
        int Merged,
        int Filtered,
        bool Succeeded)
        where TPlayable : Playable;
}
