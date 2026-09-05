using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Enrichers;

/// <summary>
/// Decorator over <see cref="EpisodeEnricher"/> that applies non-podcast overwrite
/// behaviour (refresh-meta) instead of fill-missing-only enrichment.
/// Registered as <see cref="IEpisodeEnricher"/> when SubmitUrl is started with <c>-r</c>.
/// </summary>
public sealed class RefreshMetaEpisodeEnricher(
    EpisodeEnricher inner,
    IDescriptionHelper descriptionHelper,
    ILogger<RefreshMetaEpisodeEnricher> logger) : IEpisodeEnricher
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
            if (matchingEpisode != null)
            {
                logger.LogWarning(
                    "Refresh-meta: no resolved non-podcast item for episode '{EpisodeId}' — nothing to compare or overwrite.",
                    matchingEpisode.Id);
            }

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
        // Collapse-only: do not call EnrichMissingDescription — that fills from other
        // platforms and would overwrite a good stored description when scrape is empty.
        var description = descriptionHelper.CollapseDescription(item.Description);

        var streamingKey = NonPodcastServiceKeys.Resolve(item);
        var existingImage = streamingKey != null
            ? EpisodeServicePresence.TryGetImage(matchingEpisode, streamingKey)
            : null;
        var existingUrl = streamingKey != null
            ? EpisodeServicePresence.TryGetUrl(matchingEpisode, streamingKey)
            : null;

        var updates = new List<string>();
        var changeCount = 0;

        void Note(string field, string? from, string? to, bool willUpdate)
        {
            if (willUpdate)
            {
                changeCount++;
                updates.Add($"{field}: '{NullDisplay(from)}' -> '{NullDisplay(to)}'");
            }
            else
            {
                var reason = string.IsNullOrWhiteSpace(to)
                    ? " — extract had no new value"
                    : " — extract matches stored";
                updates.Add($"{field}: unchanged ('{NullDisplay(from)}'){reason}");
            }
        }

        var titleUpdate = !string.IsNullOrWhiteSpace(item.Title) &&
                          !string.Equals(matchingEpisode.Title, item.Title, StringComparison.Ordinal);
        Note("title", matchingEpisode.Title, item.Title, titleUpdate);

        var descriptionUpdate = !string.IsNullOrWhiteSpace(description) &&
                                !string.Equals(matchingEpisode.Description, description, StringComparison.Ordinal);
        Note("description", Truncate(matchingEpisode.Description), Truncate(description), descriptionUpdate);

        var releaseUpdate = item.Release is { } release && matchingEpisode.Release != release;
        Note(
            "release",
            matchingEpisode.Release.ToString("o"),
            item.Release?.ToString("o"),
            releaseUpdate);

        var lengthUpdate = item.Duration is { } duration &&
                           duration > TimeSpan.Zero &&
                           matchingEpisode.Length != duration;
        Note(
            "length",
            matchingEpisode.Length.ToString(),
            item.Duration?.ToString(),
            lengthUpdate);

        var imageUpdate = item.Image is not null &&
                          (existingImage is null || !UriEquals(existingImage, item.Image));
        Note(
            "image",
            existingImage?.ToString(),
            item.Image?.ToString(),
            imageUpdate);

        var urlUpdate = item.Url is not null && streamingKey != null &&
                        (existingUrl is null || !UriEquals(existingUrl, item.Url));
        Note(
            "url",
            existingUrl?.ToString(),
            item.Url?.ToString(),
            urlUpdate);

        logger.LogInformation(
            "Refresh-meta plan for episode '{EpisodeId}' (service={Service}): {Plan}",
            matchingEpisode.Id,
            item.NonPodcastService,
            string.Join("; ", updates));

        var changed = false;

        if (titleUpdate)
        {
            matchingEpisode.Title = item.Title!;
            changed = true;
        }

        if (descriptionUpdate)
        {
            matchingEpisode.Description = description!;
            changed = true;
        }

        if (releaseUpdate)
        {
            matchingEpisode.Release = item.Release!.Value;
            changed = true;
        }

        if (lengthUpdate)
        {
            matchingEpisode.Length = item.Duration!.Value;
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
        else if (item.Url is { } streamingUrl && streamingKey != null)
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
        else if (item.Image is { } imageOnly)
        {
            var imageKey = ResolveNonPodcastImageKey(item);
            if (imageKey != null)
            {
                var existingServiceUrl = EpisodeServicePresence.TryGetUrl(matchingEpisode, imageKey);
                var existingServiceImage = EpisodeServicePresence.TryGetImage(matchingEpisode, imageKey);
                if (existingServiceImage is null || !UriEquals(existingServiceImage, imageOnly))
                {
                    EpisodeServicePresence.Upsert(matchingEpisode, imageKey, existingServiceUrl, imageOnly);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            episodeResult = SubmitResultState.Enriched;
            logger.LogInformation(
                "Refresh-meta applied {ChangeCount} field(s) on episode '{EpisodeId}'.",
                changeCount,
                matchingEpisode.Id);
        }
        else
        {
            logger.LogWarning(
                "Refresh-meta applied nothing on episode '{EpisodeId}'. Extracted title='{Title}', duration={Duration}, release={Release}, image='{Image}', url='{Url}'.",
                matchingEpisode.Id,
                item.Title,
                item.Duration?.ToString() ?? "(none)",
                item.Release?.ToString("o") ?? "(none)",
                item.Image?.ToString() ?? "(none)",
                item.Url?.ToString() ?? "(none)");
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

    private static string? ResolveNonPodcastImageKey(ResolvedNonPodcastServiceItem item) =>
        NonPodcastServiceKeys.Resolve(item);

    private static string NullDisplay(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value;

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        const int max = 80;
        return value.Length <= max ? value : value[..max] + "…";
    }
}
