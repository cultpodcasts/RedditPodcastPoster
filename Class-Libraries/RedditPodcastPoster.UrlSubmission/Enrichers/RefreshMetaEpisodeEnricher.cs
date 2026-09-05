using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Enrichers;

/// <summary>
/// Decorator over <see cref="IEpisodeEnricher"/> that applies non-podcast overwrite
/// behaviour (refresh-meta) instead of fill-missing-only enrichment.
/// </summary>
public sealed class RefreshMetaEpisodeEnricher(
    EpisodeEnricher inner,
    IDescriptionHelper descriptionHelper,
    ILogger<RefreshMetaEpisodeEnricher> logger) : IRefreshMetaEpisodeEnricher
{
    public ApplyResolvePodcastServicePropertiesResponse ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        CategorisedItem categorisedItem,
        Episode? matchingEpisode)
    {
        // Let the inner enricher handle podcast-platform links without running its
        // fill-missing non-podcast path; we apply overwrite ourselves below.
        var withoutNonPodcast = categorisedItem with { ResolvedNonPodcastServiceItem = null };
        var response = inner.ApplyResolvedPodcastServiceProperties(
            matchingPodcast,
            withoutNonPodcast,
            matchingEpisode);

        if (categorisedItem.ResolvedNonPodcastServiceItem == null || matchingEpisode == null)
        {
            return response;
        }

        var addedBBC = response.SubmitEpisodeDetails.BBC;
        var addedInternetArchive = response.SubmitEpisodeDetails.InternetArchive;
        var addedExtraKeys = new HashSet<string>(
            response.SubmitEpisodeDetails.ExtraServiceKeys ?? [],
            StringComparer.Ordinal);

        var episodeResult = ApplyNonPodcastRefreshMeta(
            matchingEpisode,
            categorisedItem,
            addedExtraKeys,
            ref addedBBC,
            ref addedInternetArchive,
            response.AppliedEpisodeResult);

        var details = response.SubmitEpisodeDetails with
        {
            BBC = addedBBC,
            InternetArchive = addedInternetArchive,
            Vimeo = addedExtraKeys.Contains(ServiceKeys.Vimeo) || response.SubmitEpisodeDetails.Vimeo,
            Netflix = addedExtraKeys.Contains(ServiceKeys.Netflix) || response.SubmitEpisodeDetails.Netflix,
            AmazonPrime = addedExtraKeys.Contains(ServiceKeys.AmazonPrime) ||
                          response.SubmitEpisodeDetails.AmazonPrime,
            ExtraServiceKeys = addedExtraKeys.Count == 0 ? null : addedExtraKeys.ToArray()
        };

        return response with
        {
            AppliedEpisodeResult = episodeResult,
            SubmitEpisodeDetails = details
        };
    }

    private SubmitResultState ApplyNonPodcastRefreshMeta(
        Episode matchingEpisode,
        CategorisedItem categorisedItem,
        HashSet<string> addedExtraKeys,
        ref bool addedBBC,
        ref bool addedInternetArchive,
        SubmitResultState episodeResult)
    {
        var item = categorisedItem.ResolvedNonPodcastServiceItem!;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(item.Title) &&
            !string.Equals(matchingEpisode.Title, item.Title, StringComparison.Ordinal))
        {
            matchingEpisode.Title = item.Title;
            changed = true;
        }

        var description =
            descriptionHelper.CollapseDescription(item.Description) ??
            descriptionHelper.EnrichMissingDescription(categorisedItem);
        if (!string.IsNullOrWhiteSpace(description) &&
            !string.Equals(matchingEpisode.Description, description, StringComparison.Ordinal))
        {
            matchingEpisode.Description = description;
            changed = true;
        }

        if (item.Release is { } release && matchingEpisode.Release != release)
        {
            matchingEpisode.Release = release;
            changed = true;
        }

        if (item.Duration is { } duration &&
            duration > TimeSpan.Zero &&
            matchingEpisode.Length != duration)
        {
            matchingEpisode.Length = duration;
            changed = true;
        }

        if (item.BBCUrl is { } bbcUrl)
        {
            var bbcKey = ServiceCatalog.TryResolveKey(bbcUrl) ?? ServiceKeys.BbcSounds;
            var upsert = ApplyServiceUpsertIfChanged(
                matchingEpisode,
                bbcKey,
                bbcUrl,
                item.Image);
            if (upsert.UrlWasMissing)
            {
                addedBBC = true;
                addedExtraKeys.Add(bbcKey);
            }

            changed |= upsert.Changed;
        }
        else if (item.InternetArchiveUrl is { } internetArchiveUrl)
        {
            var upsert = ApplyServiceUpsertIfChanged(
                matchingEpisode,
                ServiceKeys.InternetArchive,
                internetArchiveUrl,
                item.Image);
            if (upsert.UrlWasMissing)
            {
                addedInternetArchive = true;
                addedExtraKeys.Add(ServiceKeys.InternetArchive);
            }

            changed |= upsert.Changed;
        }
        else if (item.Url is { } streamingUrl)
        {
            var streamingKey = ServiceCatalog.TryResolveKey(streamingUrl)
                               ?? ServiceCatalog.KeyFromUnknownHost(streamingUrl);
            if (streamingKey != null)
            {
                var upsert = ApplyServiceUpsertIfChanged(
                    matchingEpisode,
                    streamingKey,
                    streamingUrl,
                    item.Image);
                if (upsert.UrlWasMissing)
                {
                    addedExtraKeys.Add(streamingKey);
                }

                changed |= upsert.Changed;
            }
        }
        else if (item.Image is { } imageOnly)
        {
            var imageKey = ResolveNonPodcastImageKey(item);
            if (imageKey != null)
            {
                var existingUrl = EpisodeServicePresence.TryGetUrl(matchingEpisode, imageKey);
                var existingImage = EpisodeServicePresence.TryGetImage(matchingEpisode, imageKey);
                if (existingImage is null || !UriEquals(existingImage, imageOnly))
                {
                    EpisodeServicePresence.Upsert(matchingEpisode, imageKey, existingUrl, imageOnly);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            episodeResult = SubmitResultState.Enriched;
            logger.LogInformation(
                "Refresh-meta overwrote non-podcast fields on episode '{matchingEpisodeId}'.",
                matchingEpisode.Id);
        }

        return episodeResult;
    }

    private static ServiceUpsertOutcome ApplyServiceUpsertIfChanged(
        Episode matchingEpisode,
        string key,
        Uri url,
        Uri? image)
    {
        var existingUrl = EpisodeServicePresence.TryGetUrl(matchingEpisode, key);
        var existingImage = EpisodeServicePresence.TryGetImage(matchingEpisode, key);
        var urlWasMissing = existingUrl is null;
        var urlChanged = existingUrl is not null && !UriEquals(existingUrl, url);
        var imageChanged = image is not null &&
                           (existingImage is null || !UriEquals(existingImage, image));

        if (!urlWasMissing && !urlChanged && !imageChanged)
        {
            return new ServiceUpsertOutcome(Changed: false, UrlWasMissing: false);
        }

        EpisodeServicePresence.Upsert(matchingEpisode, key, url, image);
        return new ServiceUpsertOutcome(Changed: true, UrlWasMissing: urlWasMissing);
    }

    private readonly record struct ServiceUpsertOutcome(bool Changed, bool UrlWasMissing);

    private static bool UriEquals(Uri left, Uri right) =>
        Uri.Compare(
            left,
            right,
            UriComponents.AbsoluteUri,
            UriFormat.UriEscaped,
            StringComparison.OrdinalIgnoreCase) == 0;

    private static string? ResolveNonPodcastImageKey(ResolvedNonPodcastServiceItem item)
    {
        if (item.BBCUrl is { } bbcForImage)
        {
            return ServiceCatalog.TryResolveKey(bbcForImage) ?? ServiceKeys.BbcSounds;
        }

        if (item.InternetArchiveUrl != null)
        {
            return ServiceKeys.InternetArchive;
        }

        if (item.Url is { } url)
        {
            return ServiceCatalog.TryResolveKey(url) ?? ServiceCatalog.KeyFromUnknownHost(url);
        }

        return null;
    }
}
