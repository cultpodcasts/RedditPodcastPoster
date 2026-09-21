using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.InternetArchive.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace SubmitUrl;

public class SubmitUrlProcessor(
    IUrlSubmitter urlSubmitter,
    IEpisodeRepository episodeRepository,
    IEpisodeSearchIndexerService episodeSearchIndexer,
    HttpClient httpClient,
    IInternetArchivePlayListProvider internetArchivePlayListProvider,
    ILogger<SubmitUrlProcessor> logger)
{
    public async Task Process(SubmitUrlRequest request)
    {
        var episodeIds = (request.EpisodeIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (episodeIds.Length > 0)
        {
            ValidateEpisodeIdMode(request);
        }
        else if (string.IsNullOrWhiteSpace(request.UrlOrFile))
        {
            throw new InvalidOperationException(
                "Provide a url/file positional argument, or one or more --episode-id values with -r.");
        }

        var indexOptions = new IndexingContext { SkipPodcastDiscovery = false };
        if (request.AllowExpensiveQueries)
        {
            indexOptions = indexOptions with
            {
                SkipExpensiveYouTubeQueries = false,
                SkipExpensiveSpotifyQueries = false
            };
        }

        string[] urls;
        if (episodeIds.Length > 0)
        {
            urls = await ResolveUrlsFromEpisodeIds(episodeIds);
        }
        else if (request.IsInternetArchivePlaylist &&
                 Uri.TryCreate(request.UrlOrFile, UriKind.Absolute, out var playlistUrl) &&
                 InternetArchiveUrlMatcher.IsInternetArchiveUrl(playlistUrl))
        {
            var pageResponse = await httpClient.GetAsync(playlistUrl);
            var document = new HtmlDocument();
            document.Load(await pageResponse.Content.ReadAsStreamAsync());
            var playlist = internetArchivePlayListProvider.GetPlayList(document);
            urls = playlist.Select(x => new Uri(playlistUrl, x.Orig).ToString()).ToArray();
        }
        else if (!request.SubmitUrlsInFile)
        {
            urls = [request.UrlOrFile!];
        }
        else
        {
            urls = await File.ReadAllLinesAsync(request.UrlOrFile!);
        }

        var updatedEpisodeIds = new List<Guid>();

        foreach (var url in urls)
        {
            logger.LogInformation("Ingesting '{url}'.", url);
            // -r / RefreshMeta: Program registers overwrite enricher via
            // AddUrlSubmission(useRefreshMetaEnricher); this flag only forces known-URL meta extract.
            var result = await urlSubmitter.Submit(
                new Uri(url, UriKind.Absolute),
                indexOptions,
                new SubmitOptions(
                    request.PodcastId,
                    request.MatchOtherServices,
                    !request.DryRun,
                    request.CreatePodcast,
                    request.PodcastName,
                    RefreshMeta: request.RefreshMeta));
            logger.LogInformation(result.ToString());
            if (result.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                updatedEpisodeIds.Add(result.Episode!.Id);
            }
        }

        updatedEpisodeIds = updatedEpisodeIds.Distinct().ToList();
        if (!request.NoIndex && updatedEpisodeIds.Count > 0)
        {
            try
            {
                await episodeSearchIndexer.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failure indexing changes.");
            }
        }
    }

    private static void ValidateEpisodeIdMode(SubmitUrlRequest request) =>
        SubmitUrlEpisodeIdModeValidator.EnsureValid(
            request.RefreshMeta,
            request.SubmitUrlsInFile,
            request.IsInternetArchivePlaylist,
            request.CreatePodcast,
            request.UrlOrFile);

    private async Task<string[]> ResolveUrlsFromEpisodeIds(Guid[] episodeIds)
    {
        var urls = new List<string>(episodeIds.Length);
        foreach (var episodeId in episodeIds)
        {
            var episode = await episodeRepository.GetBy(e => e.Id == episodeId);
            if (episode is null)
            {
                throw new InvalidOperationException($"Episode '{episodeId}' was not found.");
            }

            if (!SubmitUrlStreamingUrlResolver.TryGetUrl(episode, out var url))
            {
                throw new InvalidOperationException(
                    $"Episode '{episodeId}' has no streaming services.*.url to refresh from " +
                    "(Spotify/Apple/YouTube alone are not used).");
            }

            logger.LogInformation(
                "Resolved episode '{EpisodeId}' ({PodcastName}) to streaming URL '{Url}'.",
                episodeId,
                episode.PodcastName,
                url);
            urls.Add(url.ToString());
        }

        return urls.ToArray();
    }
}
