using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.UrlSubmission.Languages;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Services.SubmitUrl;

public class SubmitUrlService(
    IPodcastRepository repository,
    IUrlSubmitter urlSubmitter,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILookupRepository lookupRepository,
    ILogger<SubmitUrlService> logger,
    IPlayableSearchDocumentSwap playableSearch) : ISubmitUrlService
{
    public async Task<SubmitUrlResult> SubmitAsync(
        SubmitUrlRequest submitUrlModel,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "{RunName}: Handling url-submission: url: '{Url}', podcast-id: '{PodcastId}', podcast-name: '{PodcastName}'.",
                nameof(SubmitAsync), submitUrlModel.Url, submitUrlModel.PodcastId, submitUrlModel.PodcastName);
            var parentTarget = submitUrlModel.GetParentTarget(out var parentError);
            if (parentError != null)
            {
                logger.LogWarning("{RunName}: Rejected submit of '{Url}': {Error}", nameof(SubmitAsync),
                    submitUrlModel.Url, parentError);
                return new SubmitUrlResult(SubmitUrlStatus.BadRequest, Message: parentError);
            }

            if (parentTarget != null && !UrlSubmitter.IsPodcastPlatformUrl(submitUrlModel.Url))
            {
                return new SubmitUrlResult(SubmitUrlStatus.BadRequest,
                    Message: "Only Spotify, YouTube or Apple Podcasts URLs can be filed under a news organisation or TV show.");
            }

            if (parentTarget != null)
            {
                logger.LogInformation(
                    "{RunName}: Filing '{Url}' under {Kind} '{ParentId}' ('{ParentName}').",
                    nameof(SubmitAsync), submitUrlModel.Url, parentTarget.Kind, parentTarget.Id, parentTarget.Name);
            }

            Guid? podcastId = submitUrlModel.PodcastId;
            // Name-only: unique → attach by id; many → 409; none → leave id unset so ingest creates a series with PodcastName.
            if (podcastId == null && !string.IsNullOrWhiteSpace(submitUrlModel.PodcastName))
            {
                var matches = await PodcastNameAttachLookup.FindByName(
                    repository,
                    submitUrlModel.PodcastName,
                    cancellationToken);

                if (matches.Count > 1)
                {
                    logger.LogWarning(
                        "{RunName}: Podcast name '{PodcastName}' matches {Count} rows; refusing first-iterator attach. Ids: {Ids}.",
                        nameof(SubmitAsync),
                        submitUrlModel.PodcastName,
                        matches.Count,
                        string.Join(", ", matches.Select(x => x.Id)));
                    return new SubmitUrlResult(
                        SubmitUrlStatus.Conflict,
                        AmbiguousPodcasts: matches.Select(x => x.Id));
                }

                if (matches.Count == 1)
                {
                    podcastId = matches[0].Id;
                }
            }

            var supported = await SupportedLanguageCodes();
            var choice = CatalogueLanguageChoices.Parse(submitUrlModel.LanguageDecision);
            var identityChoice = CatalogueIdentityChoices.Parse(submitUrlModel.IdentityDecision);
            var submitOptions = new SubmitOptions(
                podcastId,
                true,
                PodcastName: submitUrlModel.PodcastName,
                PrefetchedMeta: submitUrlModel.PrefetchedMeta,
                Parent: parentTarget,
                EnforceLanguageGate: true,
                SupportedLanguageCodes: supported,
                LanguageChoice: choice,
                IdentityChoice: identityChoice,
                SelectedIdentityKey: submitUrlModel.SelectedIdentityKey);
            var result = await urlSubmitter.Submit(
                submitUrlModel.Url,
                new IndexingContext
                {
                    SkipPodcastDiscovery = false,
                    SkipExpensiveYouTubeQueries = false,
                    SkipExpensiveSpotifyQueries = false
                },
                submitOptions);

            if (result.Rejected)
            {
                return new SubmitUrlResult(SubmitUrlStatus.Rejected, result);
            }

            if (result.RequiresCurator)
            {
                return new SubmitUrlResult(SubmitUrlStatus.RequiresCurator, result);
            }

            if (choice == CatalogueLanguageChoice.Cancel || identityChoice == CatalogueIdentityChoice.Cancel)
            {
                return new SubmitUrlResult(SubmitUrlStatus.Cancelled, result);
            }

            if (result.LanguageHold is { Code: { Length: > 0 } })
            {
                return new SubmitUrlResult(SubmitUrlStatus.LanguageDecisionRequired, result);
            }

            if (result.IdentityHold is { Candidates.Count: > 0 })
            {
                return new SubmitUrlResult(SubmitUrlStatus.IdentityDecisionRequired, result);
            }

            var episodeId = result.Episode?.Id;
            if (result.CatalogueSearchSource is { } searchSource)
            {
                await IndexCatalogueParentItem(searchSource, cancellationToken);
            }

            if (result.PlayableId.HasValue && result.Episode == null)
            {
                return new SubmitUrlResult(SubmitUrlStatus.Ok, result);
            }

            if (result.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                if (episodeId.HasValue)
                {
                    try
                    {
                        await episodeSearchIndexerService.IndexEpisode(episodeId.Value, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to index episode after submission. EpisodeId: '{EpisodeId}'.",
                            episodeId.Value);
                    }
                }
                else
                {
                    logger.LogError(
                        "Submit result indicated episode state '{EpisodeResult}' but no episode id was returned. Url: '{Url}'.",
                        result.EpisodeResult,
                        submitUrlModel.Url);
                }
            }

            return new SubmitUrlResult(SubmitUrlStatus.Ok, result);
        }
        catch (AmbiguousPodcastNameException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Ambiguous podcast name '{PodcastName}' on submit of '{Url}'.",
                nameof(SubmitAsync),
                ex.PodcastName,
                submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.Conflict, AmbiguousPodcasts: ex.PodcastIds);
        }
        catch (AmbiguousParentNameException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Ambiguous {ContentKind} parent '{ParentName}' on submit of '{Url}'.",
                nameof(SubmitAsync),
                ex.ContentKind,
                ex.ParentName,
                submitUrlModel.Url);
            return new SubmitUrlResult(
                SubmitUrlStatus.Conflict,
                AmbiguousPodcasts: ex.ParentIds,
                ContentKind: ex.ContentKind,
                ParentName: ex.ParentName);
        }
        catch (SubmitParentNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Submit referenced missing {Kind} id '{ParentId}' for '{Url}'.",
                nameof(SubmitAsync),
                ex.Kind,
                ex.ParentId,
                submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.ParentNotFound, Message: $"{ex.Kind} not found");
        }
        catch (SubmitPodcastNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Submit referenced missing podcast id '{PodcastId}' for '{Url}'.",
                nameof(SubmitAsync),
                ex.PodcastId,
                submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.PodcastNotFound, Message: "Podcast not found");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Submit of '{Url}' could not be stored. {Message}",
                nameof(SubmitAsync),
                submitUrlModel.Url,
                ex.Message);
            return new SubmitUrlResult(SubmitUrlStatus.Failed, Message: ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RunName}: Failed to submit url '{Url}'.", nameof(SubmitAsync), submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.Failed, Message: "Failure");
        }
    }

    /// <summary>A URL routed to a news organisation / TV show is indexed now, like a podcast episode.</summary>
    private Task IndexCatalogueParentItem(CatalogueSearchSource source, CancellationToken cancellationToken) =>
        CatalogueSubmitSearchIndexing.IndexAsync(playableSearch, source, logger, cancellationToken);

    private async Task<IReadOnlyCollection<string>> SupportedLanguageCodes()
    {
        var pending = lookupRepository.GetSupportedLanguagesConfig();
        var config = pending == null ? null : await pending;
        config ??= SupportedLanguagesConfig.CreateDefault();
        return config.Languages.Select(x => x.Code).ToArray();
    }
}
