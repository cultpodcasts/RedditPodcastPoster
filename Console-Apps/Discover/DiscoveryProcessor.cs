using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public class DiscoveryProcessor(
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    IDiscoveryResultConsoleLogger discoveryResultConsoleLogger,
    IDiscoveryResultsRepository discoveryResultsRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private static readonly TimeSpan Since = TimeSpan.FromHours(6);

    public async Task<DiscoveryResponse> Process(DiscoveryRequest request)
    {
        var fg = Console.ForegroundColor;

        IList<DiscoveryResult> discoveryResults;
        DateTime? latest;
        List<Guid>? unprocessedEpisodeIds = null;
        if (request.UseRemote)
        {
            unprocessedEpisodeIds = [];
            discoveryResults = await GetRemoteDiscoveryResults(unprocessedEpisodeIds);
            latest = discoveryResults.LastOrDefault()?.Released;
        }
        else
        {
            discoveryResults = await GetDiscoveryResults(request, request.Since??DateTime.UtcNow.Subtract(Since));
            var localDiscoveryContext = CreateDiscoveryContext(request);
            await SaveDiscoveryResults(request, localDiscoveryContext, discoveryResults);
            latest = localDiscoveryContext.DiscoveryBegan;
        }

        foreach (var episode in discoveryResults)
        {
            discoveryResultConsoleLogger.DisplayEpisode(episode, fg);
        }

        if (request.UseRemote)
        {
            logger.LogInformation(
                "{method}: marking {documentType} ids as processed: {documentIds}.",
                nameof(Process), nameof(DiscoveryResultsDocument), string.Join(", ", unprocessedEpisodeIds!));
            await discoveryResultsRepository.SetProcessed(unprocessedEpisodeIds!);
        }

        return new DiscoveryResponse(latest);
    }

    private (DateTime Since, string SearchSince, DateTime DiscoveryBegan) CreateDiscoveryContext(DiscoveryRequest request)
    {
        DateTime since;
        string searchSince;
        if (request.Since.HasValue)
        {
            if (request.Since.Value.ToUniversalTime() > DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    $"'{nameof(request)}.{nameof(request.Since)}' is in the future. ");
            }

            since = request.Since.Value.ToUniversalTime();
            searchSince = DateTime.UtcNow.Subtract(since).ToString();
        }
        else if (request.NumberOfHours.HasValue)
        {
            since = DateTime.UtcNow.Subtract(TimeSpan.FromHours(request.NumberOfHours.Value));
            searchSince = TimeSpan.FromHours(request.NumberOfHours.Value).ToString();
        }
        else
        {
            throw new InvalidOperationException("Unable to determine baseline-time to discover from.");
        }

        Console.WriteLine(
            $"Discovering items released since '{since.ToUniversalTime():O}' (local:'{since.ToLocalTime():O}').");

        var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
        Console.WriteLine(
            $"Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBegan.ToLocalTime():O}').");

        return (since, searchSince, discoveryBegan);
    }

    private async Task<List<DiscoveryResult>> GetDiscoveryResults(DiscoveryRequest request, DateTime since)
    {
        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var discoveryConfig = discoveryConfigProvider.CreateDiscoveryConfig(
            new GetServiceConfigOptions(since, request.ExcludeSpotify, request.IncludeYouTube,
                request.IncludeListenNotes, request.IncludeTaddy, request.EnrichFromSpotify,
                request.EnrichFromApple, request.TaddyOffset));

        return await discoveryService
            .GetDiscoveryResults(discoveryConfig, indexingContext)
            .ToListAsync();
    }

    private async Task SaveDiscoveryResults(
        DiscoveryRequest request,
        (DateTime Since, string SearchSince, DateTime DiscoveryBegan) discoveryContext,
        IList<DiscoveryResult> discoveryResults)
    {
        var indexingContext = new IndexingContext(
            discoveryContext.Since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var discoveryResultsDocument = new DiscoveryResultsDocument(discoveryContext.DiscoveryBegan, discoveryResults)
        {
            SearchSince = discoveryContext.SearchSince,
            ExcludeSpotify = request.ExcludeSpotify,
            IncludeYouTube = request.IncludeYouTube,
            IncludeListenNotes = request.IncludeListenNotes,
            IncludeTaddy = request.IncludeTaddy,
            EnrichFromSpotify = request.EnrichFromSpotify,
            EnrichFromApple = request.EnrichFromApple,
            PreSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving,
            PostSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving
        };

        await discoveryResultsRepository.Save(discoveryResultsDocument);
        logger.LogInformation(
            "{method}: persisted {documentType} with id '{documentId}'.",
            nameof(Process), nameof(DiscoveryResultsDocument), discoveryResultsDocument.Id);
    }

    private async Task<List<DiscoveryResult>> GetRemoteDiscoveryResults(List<Guid> unprocessedEpisodeIds)
    {
        var remoteDiscoveryResults = new List<DiscoveryResult>();
        await foreach (var report in discoveryResultsRepository.GetAllUnprocessed())
        {
            unprocessedEpisodeIds.Add(report.Id);
            remoteDiscoveryResults.AddRange(report.DiscoveryResults);
        }

        return remoteDiscoveryResults.OrderBy(x => x.Released).ToList();
    }
}