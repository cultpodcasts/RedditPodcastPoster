using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
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
/// </summary>
public class CatalogueParentSubmitter(
    IUrlCategoriser urlCategoriser,
    IPodcastProcessor podcastProcessor,
    INewsReportRepository newsReports,
    ITvShowEpisodeRepository tvShowEpisodes,
    ILogger<CatalogueParentSubmitter> logger) : ICatalogueParentSubmitter
{
    public async Task<SubmitResult> Submit(
        CatalogueParentMatch parent,
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var transient = (parent.Indexing ?? new CatalogueIndexingSource()).ToTransientPodcast(parent.Parent);
        var categorisedItem = await urlCategoriser.Categorise(
            transient,
            url,
            indexingContext,
            submitOptions.MatchOtherServices,
            submitOptions.PrefetchedMeta,
            forceMetaExtract: submitOptions.RefreshMeta);

        var processed = await podcastProcessor.AddEpisodeToExistingPodcast(categorisedItem);
        if (processed is not { EpisodeResult: SubmitResultState.Created, Episode: { } episode })
        {
            logger.LogWarning(
                "Submit of '{Url}' to {Kind} '{ParentName}' ({ParentId}) produced no new item (episode state '{EpisodeResult}').",
                url, parent.Kind, parent.Parent.Name, parent.Parent.Id, processed?.EpisodeResult);
            return new SubmitResult(
                processed?.EpisodeResult ?? SubmitResultState.None,
                SubmitResultState.None,
                ContentKind: parent.ContentKind);
        }

        var existingId = await FindExisting(parent, episode);
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

        switch (playable)
        {
            case NewsReport report:
                await newsReports.Save(report);
                break;
            case TvShowEpisode tvShowEpisode:
                await tvShowEpisodes.Save(tvShowEpisode);
                break;
        }

        logger.LogInformation(
            "Submit of '{Url}' stored {ContentKind} '{PlayableId}' under {Kind} '{ParentName}' ({ParentId}); no podcast created.",
            url, parent.ContentKind, playable.Id, parent.Kind, parent.Parent.Name, parent.Parent.Id);
        return new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            processed.SubmitEpisodeDetails,
            ContentKind: parent.ContentKind,
            PlayableId: playable.Id,
            CatalogueSearchSource: new CatalogueSearchSource(transient, episode, parent.Kind));
    }

    /// <summary>A playable of this parent that already has one of the new item's service urls.</summary>
    private async Task<Guid?> FindExisting(CatalogueParentMatch parent, Episode episode)
    {
        if (episode.Services is not { Count: > 0 } services)
        {
            return null;
        }

        var parentId = parent.Parent.Id;
        foreach (var (key, link) in services)
        {
            if (link?.Url is not { } serviceUrl)
            {
                continue;
            }

            Playable? existing = parent.Kind == CatalogueParentKind.NewsOrganisation
                ? await newsReports.GetBy(x =>
                    x.NewsOrganisationId == parentId && x.Services != null && x.Services[key].Url == serviceUrl)
                : await tvShowEpisodes.GetBy(x =>
                    x.TvShowId == parentId && x.Services != null && x.Services[key].Url == serviceUrl);
            if (existing != null)
            {
                return existing.Id;
            }
        }

        return null;
    }
}
