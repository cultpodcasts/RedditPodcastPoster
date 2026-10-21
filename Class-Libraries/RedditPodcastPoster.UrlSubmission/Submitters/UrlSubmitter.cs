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
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.UrlSubmission.Submitters;

public class UrlSubmitter(
    IPodcastRepository podcastRepository,
    IPodcastService podcastService,
    IUrlCategoriser urlCategoriser,
    ICategorisedItemProcessor categorisedItemProcessor,
    ILogger<UrlSubmitter> logger,
    IOptions<SubmitContentTypesOptions>? submitContentTypes = null,
    ICatalogueParentLookup? catalogueParentLookup = null,
    ICatalogueParentSubmitter? catalogueParentSubmitter = null)
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

            Podcast? podcast = null;
            CatalogueParentMatch? parent = null;
            EpisodeUrlOwner? owner = null;
            var routesToParents = catalogueParentLookup != null && catalogueParentSubmitter != null &&
                                  IsPodcastPlatformUrl(url);
            if (!submitOptions.CreatePodcast)
            {
                if (submitOptions.PodcastId != null)
                {
                    podcast = await podcastRepository.GetPodcast(submitOptions.PodcastId.Value);
                    if (podcast == null && routesToParents)
                    {
                        parent = await catalogueParentLookup!.FindById(submitOptions.PodcastId.Value);
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
                        parent = await catalogueParentLookup!.FindByName(submitOptions.PodcastName);
                    }
                }
                else
                {
                    owner = await podcastService.GetEpisodeUrlOwner(url, indexingContext);
                    podcast = owner?.Podcast;
                    if (podcast != null && routesToParents && owner != null)
                    {
                        (podcast, parent) = await PreferParentOverPlaylistScopedPodcast(
                            podcast, owner.Ids, url, indexingContext);
                    }
                }

                if (podcast != null && podcast.IsRemoved())
                {
                    logger.LogWarning("Podcast with id '{podcastId}' is removed.", podcast.Id);
                    return new SubmitResult(episodeResult, SubmitResultState.PodcastRemoved);
                }
            }

            if (podcast == null && parent == null && routesToParents)
            {
                // Never create a podcast for a channel / show a news organisation or TV show owns.
                owner ??= await TryGetEpisodeUrlOwner(url, indexingContext);
                if (owner != null)
                {
                    parent = await catalogueParentLookup!.FindByOwner(owner.Ids);
                }
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
                return await catalogueParentSubmitter!.Submit(parent, url, indexingContext, submitOptions);
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

    /// <summary>
    /// A podcast matched only by YouTube channel, scoped to one playlist of that channel (e.g. a show on a news
    /// outlet's channel), keeps the URL only when the video is in its playlist. Otherwise the news organisation
    /// or TV show that owns the channel gets it. With no owning parent the podcast keeps it (unchanged).
    /// </summary>
    private async Task<(Podcast? Podcast, CatalogueParentMatch? Parent)> PreferParentOverPlaylistScopedPodcast(
        Podcast podcast,
        EpisodeUrlOwnerIds ids,
        Uri url,
        IndexingContext indexingContext)
    {
        if (string.IsNullOrWhiteSpace(ids.YouTubeChannelId) ||
            string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId) ||
            podcast.YouTubeChannelId != ids.YouTubeChannelId)
        {
            return (podcast, null);
        }

        var channelOwner = await catalogueParentLookup!.FindByOwner(ids);
        if (channelOwner == null)
        {
            return (podcast, null);
        }

        var inPlaylist = await podcastService.IsVideoInPodcastPlaylist(podcast, url, indexingContext);
        if (inPlaylist == true)
        {
            return (podcast, null);
        }

        if (inPlaylist == null)
        {
            throw new InvalidOperationException(
                $"Could not check whether '{url}' is in the playlist of podcast '{podcast.Name}', which shares its channel with {channelOwner.Kind} '{channelOwner.Parent.Name}'. Retry, or submit with the podcast or {channelOwner.Kind} name.");
        }

        logger.LogInformation(
            "Video '{Url}' is not in playlist '{PlaylistId}' of podcast '{PodcastName}' ({PodcastId}); routing to channel owner {Kind} '{ParentName}' ({ParentId}).",
            url, podcast.YouTubePlaylistId, podcast.Name, podcast.Id, channelOwner.Kind, channelOwner.Parent.Name,
            channelOwner.Parent.Id);
        return (null, channelOwner);
    }

    private static bool IsPodcastPlatformUrl(Uri url) =>
        SpotifyPodcastServiceMatcher.IsMatch(url) ||
        YouTubePodcastServiceMatcher.IsMatch(url) ||
        ApplePodcastServiceMatcher.IsMatch(url);

    /// <summary>
    /// Owner ids for the name and create-podcast paths, which did not resolve them before. A lookup failure
    /// keeps the previous behaviour (no routing) instead of failing the submit.
    /// </summary>
    private async Task<EpisodeUrlOwner?> TryGetEpisodeUrlOwner(Uri url, IndexingContext indexingContext)
    {
        try
        {
            return await podcastService.GetEpisodeUrlOwner(url, indexingContext);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            logger.LogWarning(e, "Could not resolve the channel / show of '{Url}'; not routing to a news organisation or TV show.", url);
            return null;
        }
    }
}
