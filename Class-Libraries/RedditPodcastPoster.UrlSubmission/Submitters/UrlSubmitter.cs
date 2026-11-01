using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Submitters;

public class UrlSubmitter(
    IPodcastRepository podcastRepository,
    IUrlCategoriser urlCategoriser,
    ICategorisedItemProcessor categorisedItemProcessor,
    ILogger<UrlSubmitter> logger,
    ICatalogueParentLookup catalogueParentLookup,
    ICatalogueParentSubmitter catalogueParentSubmitter,
    IEpisodeUrlRouter episodeUrlRouter,
    IUrlMembershipLookup urlMembershipLookup,
    IOptions<SubmitContentTypesOptions>? submitContentTypes = null)
    : IUrlSubmitter
{
    public async Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var episodeResult = SubmitResultState.None;
        try
        {
            if (submitContentTypes?.Value is { Enabled: true } && SubmitContentClassifier.IsBbcNews(url))
            {
                return new SubmitResult(
                    SubmitResultState.None,
                    SubmitResultState.None,
                    ContentKind: SubmitClassification.NewsReport);
            }

            // An item moved out of a podcast (to a film, film trailer, TV episode or news report) keeps its
            // YouTube / Spotify / Apple URL there, and its episode row is gone. Re-submitting that URL must not
            // re-create the podcast episode (#1089). Same ownership rule as the indexer.
            var catalogueOwner = IsPodcastPlatformUrl(url)
                ? await urlMembershipLookup.FindCatalogueServiceUrlOwner(url, CancellationToken.None)
                : null;
            if (catalogueOwner != null && catalogueOwner.ContentKind == SubmitClassification.Film)
            {
                // Films (and their trailers) have no podcast-platform resubmit path, so a film owner always stops here.
                return CatalogueOwned(url, catalogueOwner);
            }

            if (submitOptions.Parent is { } target)
            {
                return await SubmitToParentTarget(target, url, indexingContext, submitOptions);
            }

            Podcast? podcast = null;
            CatalogueParentMatch? parent = null;
            EpisodeUrlOwner? owner = null;
            var ownerRouted = false;
            var routesToParents = IsPodcastPlatformUrl(url);
            if (!submitOptions.CreatePodcast)
            {
                if (submitOptions.PodcastId != null)
                {
                    podcast = await podcastRepository.GetPodcast(submitOptions.PodcastId.Value);
                    if (podcast == null && routesToParents)
                    {
                        parent = await catalogueParentLookup.FindById(submitOptions.PodcastId.Value);
                    }

                    if (podcast == null && parent == null)
                    {
                        logger.LogWarning(
                            "Submit referenced podcast id '{PodcastId}' which does not exist.",
                            submitOptions.PodcastId.Value);
                        throw new SubmitPodcastNotFoundException(submitOptions.PodcastId.Value);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(submitOptions.PodcastName))
                {
                    var matches = await PodcastNameAttachLookup.FindByName(
                        podcastRepository,
                        submitOptions.PodcastName);
                    if (matches.Count > 1)
                    {
                        logger.LogWarning(
                            "Podcast name '{PodcastName}' matches {Count} rows; refusing first-iterator attach. Ids: {Ids}.",
                            submitOptions.PodcastName,
                            matches.Count,
                            string.Join(", ", matches.Select(x => x.Id)));
                        throw new AmbiguousPodcastNameException(
                            submitOptions.PodcastName,
                            matches.Select(x => x.Id).ToArray());
                    }

                    podcast = matches.Count == 1 ? matches[0] : null;
                    if (podcast == null && routesToParents)
                    {
                        // No podcast has the name: target a news organisation or TV show with it.
                        parent = await catalogueParentLookup.FindByName(submitOptions.PodcastName);
                    }
                }
                else
                {
                    // Same channel-owner rule as discovery (IEpisodeUrlRouter).
                    var route = await episodeUrlRouter.RouteByOwner(url, indexingContext);
                    owner = route.Owner;
                    podcast = route.Podcast;
                    parent = route.Parent;
                    ownerRouted = true;
                }

                if (podcast != null && podcast.IsRemoved())
                {
                    logger.LogWarning("Podcast with id '{podcastId}' is removed.", podcast.Id);
                    return new SubmitResult(episodeResult, SubmitResultState.PodcastRemoved);
                }
            }

            if (podcast == null && parent == null && routesToParents && !ownerRouted)
            {
                // Never create a podcast for a channel / show a news organisation or TV show owns.
                parent = await episodeUrlRouter.FindOwningParent(url, owner, indexingContext);
            }

            if (parent == null && catalogueOwner != null)
            {
                // A TV episode / news report owns the URL but routing found no parent to re-submit it under (that
                // path matches and enriches the stored item). Never file it as a podcast episode.
                return CatalogueOwned(url, catalogueOwner);
            }

            if (parent != null)
            {
                if (parent.Parent.IsRemoved())
                {
                    logger.LogWarning("{Kind} with id '{ParentId}' is removed.", parent.Kind, parent.Parent.Id);
                    return new SubmitResult(
                        episodeResult,
                        SubmitResultState.PodcastRemoved,
                        ContentKind: parent.ContentKind);
                }

                logger.LogInformation(
                    "Submit of '{Url}' routed to {Kind} '{ParentName}' ({ParentId}).",
                    url, parent.Kind, parent.Parent.Name, parent.Parent.Id);
                return await catalogueParentSubmitter.Submit(parent, url, indexingContext, submitOptions);
            }

            var categorisedItem =
                await urlCategoriser.Categorise(
                    podcast,
                    url,
                    indexingContext,
                    submitOptions.MatchOtherServices,
                    submitOptions.PrefetchedMeta,
                    forceMetaExtract: submitOptions.RefreshMeta);

            if (submitContentTypes?.Value?.Enabled == true && submitOptions.ClassificationSignals is null)
            {
                submitOptions = submitOptions with
                {
                    ClassificationSignals = SubmitContentClassifier.FromSubmission(url, categorisedItem)
                };
            }

            var submitResult = await categorisedItemProcessor.ProcessCategorisedItem(categorisedItem, submitOptions);

            if (submitResult.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                if (submitResult.Episode == null && submitResult.PlayableId == null)
                {
                    logger.LogError(
                        "Submit completed with episode state '{EpisodeResult}' but no episode instance. Url: '{Url}', PodcastId: '{PodcastId}', CreatePodcast: {CreatePodcast}. Result: {SubmitResult}.",
                        submitResult.EpisodeResult,
                        url,
                        submitOptions.PodcastId,
                        submitOptions.CreatePodcast,
                        submitResult);
                }
                else if (submitResult.Episode != null)
                {
                    logger.LogInformation(
                        "Submit completed with episode state '{EpisodeResult}' and episode id '{EpisodeId}'. Url: '{Url}'.",
                        submitResult.EpisodeResult,
                        submitResult.Episode.Id,
                        url);
                }
                else
                {
                    logger.LogInformation(
                        "Submit completed with content kind '{ContentKind}' and playable id '{PlayableId}'. Url: '{Url}'.",
                        submitResult.ContentKind,
                        submitResult.PlayableId,
                        url);
                }
            }

            return submitResult;
        }
        catch (AmbiguousPodcastNameException)
        {
            throw;
        }
        catch (AmbiguousParentNameException)
        {
            throw;
        }
        catch (SubmitPodcastNotFoundException)
        {
            throw;
        }
        catch (SubmitParentNotFoundException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Error ingesting '{url}'. Http-request-exception with status: '{status}'", url,
                e.StatusCode);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error ingesting '{url}'.", url);
            return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
        }
    }

    private SubmitResult CatalogueOwned(Uri url, CatalogueServiceUrlOwner owner)
    {
        logger.LogInformation(
            "Submit of '{Url}' skipped: already in the catalogue as {ContentKind} '{PlayableId}'.",
            url, owner.ContentKind, owner.PlayableId);
        return new SubmitResult(
            SubmitResultState.EpisodeAlreadyExists,
            SubmitResultState.None,
            ContentKind: owner.ContentKind,
            PlayableId: owner.PlayableId);
    }

    /// <summary>Spotify, YouTube or Apple Podcasts: the URLs that can be routed to a news organisation or TV show.</summary>
    public static bool IsPodcastPlatformUrl(Uri url) => EpisodeUrlRouter.IsPodcastPlatformUrl(url);

    /// <summary>
    /// The curator chose the news organisation / TV show (by id). No podcast lookup, no owner routing, and never a
    /// podcast: a missing parent is <see cref="SubmitParentNotFoundException"/>.
    /// </summary>
    private async Task<SubmitResult> SubmitToParentTarget(
        CatalogueParentTarget target,
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        if (!IsPodcastPlatformUrl(url))
        {
            // SubmitUrlService answers this with a 400 first; this guard keeps other callers from reaching the categoriser.
            throw new InvalidOperationException(
                $"Only Spotify, YouTube or Apple Podcasts URLs can be filed under a {target.Kind}; got '{url}'.");
        }

        var parent = await catalogueParentLookup.FindById(target.Kind, target.Id);
        if (parent == null)
        {
            logger.LogWarning("Submit of '{Url}' targeted {Kind} '{ParentId}' which does not exist.", url, target.Kind,
                target.Id);
            throw new SubmitParentNotFoundException(target.Kind, target.Id);
        }

        if (!string.IsNullOrWhiteSpace(target.Name) &&
            !string.Equals(target.Name.Trim(), parent.Parent.Name, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Submit of '{Url}' targeted {Kind} '{ParentId}' as '{TargetName}', but it is named '{ParentName}'; filing by id.",
                url, target.Kind, target.Id, target.Name, parent.Parent.Name);
        }

        if (parent.Parent.IsRemoved())
        {
            logger.LogWarning("{Kind} with id '{ParentId}' is removed.", parent.Kind, parent.Parent.Id);
            return new SubmitResult(SubmitResultState.None, SubmitResultState.PodcastRemoved,
                ContentKind: parent.ContentKind);
        }

        logger.LogInformation("Submit of '{Url}' filed under {Kind} '{ParentName}' ({ParentId}) by the curator.",
            url, parent.Kind, parent.Parent.Name, parent.Parent.Id);
        return await catalogueParentSubmitter.Submit(parent, url, indexingContext, submitOptions);
    }
}
