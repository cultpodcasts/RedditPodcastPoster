using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Cloudflare.Clients;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.CloudflareRedirect.Configuration;
using RedditPodcastPoster.CloudflareRedirect.Models;

namespace RedditPodcastPoster.CloudflareRedirect.Services;

public class RedirectService(
    IKVClient kVClient,
    IOptions<RedirectOptions> redirectOptions,
    ILogger<RedirectService> logger) : IRedirectService
{
    private readonly RedirectOptions _redirectOptions = redirectOptions.Value;

    public async Task<bool> CreatePodcastRedirect(PodcastRedirect podcastRedirect)
    {
        if (await IsSafe(podcastRedirect))
        {
            var record = new KVRecord()
            {
                Key = podcastRedirect.OldPodcastName,
                Value = podcastRedirect.NewPodcastName
            };
            await kVClient.Write(record, _redirectOptions.KVRedirectNamespaceId);
            return true;
        }
        else
        {
            logger.LogError(
                "Unable to create podcast-redirect. Redirect considered unsafe. '{PodcastRedirectOldPodcastName}' -> '{PodcastRedirectNewPodcastName}'.", podcastRedirect.OldPodcastName, podcastRedirect.NewPodcastName);
        }
        return false;
    }

    public async Task<bool> CreateCatalogueRedirect(CatalogueRedirect redirect)
    {
        var redirects = await kVClient.GetAll(_redirectOptions.KVRedirectNamespaceId);
        if (redirects == null)
        {
            throw new InvalidOperationException("Null response when requesting all keys");
        }

        // Same rule as the podcast IsSafe: refuse when a redirect already starts at the new name (a loop).
        var newKeyLower = redirect.NewKey.ToLowerInvariant();
        if (redirects.Keys.Any(key => key.ToLowerInvariant() == newKeyLower))
        {
            logger.LogError(
                "Unable to create catalogue-redirect. Redirect considered unsafe. '{OldKey}' -> '{NewName}'.",
                redirect.Key, redirect.NewName);
            return false;
        }

        var result = await kVClient.Write(new KVRecord { Key = redirect.Key, Value = redirect.NewName },
            _redirectOptions.KVRedirectNamespaceId);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to write catalogue-redirect '{redirect.Key}'.");
        }

        return true;
    }

    private async Task<bool> IsSafe(PodcastRedirect podcastRedirect)
    {
        var allRedirects = await GetAllPodcastRedirects();
        var newLower = podcastRedirect.NewPodcastName.ToLowerInvariant();
        var oldLower = podcastRedirect.OldPodcastName.ToLowerInvariant();

        var chainElements = allRedirects.Where(x =>
            x.NewPodcastName.ToLowerInvariant() == newLower ||
            x.NewPodcastName.ToLowerInvariant() == oldLower ||
            x.OldPodcastName.ToLowerInvariant() == newLower ||
            x.OldPodcastName.ToLowerInvariant() == oldLower);

        if (chainElements.Any(x => x.OldPodcastName.ToLowerInvariant() == newLower))
        {
            return false;
        }

        return true;
    }

    private async Task<List<PodcastRedirect>> GetAllPodcastRedirects()
    {
        var redirects = await kVClient.GetAll(_redirectOptions.KVRedirectNamespaceId);
        if (redirects == null)
        {
            throw new InvalidOperationException($"Null response when requesting all keys");
        }
        return redirects.Select(x => new PodcastRedirect(x.Key, x.Value)).ToList();
    }
}
