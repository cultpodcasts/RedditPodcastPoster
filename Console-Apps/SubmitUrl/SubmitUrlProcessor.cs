using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;
using RedditPodcastPoster.UrlSubmission.Models;

namespace SubmitUrl;

public class SubmitUrlProcessor(
    IUrlSubmitter urlSubmitter,
    IEpisodeSearchIndexerService episodeSearchIndexer,
    HttpClient httpClient,
    IInternetArchivePlayListProvider internetArchivePlayListProvider,
    ILogger<SubmitUrlProcessor> logger)
{
    public async Task Process(SubmitUrlRequest request)
    {
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
        if (request.IsInternetArchivePlaylist &&
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
            urls = [request.UrlOrFile];
        }
        else
        {
            urls = await File.ReadAllLinesAsync(request.UrlOrFile);
        }

        var updatedEpisodeIds = new List<Guid>();

        foreach (var url in urls)
        {
            logger.LogInformation("Ingesting '{url}'.", url);
            var result = await urlSubmitter.Submit(
                new Uri(url, UriKind.Absolute),
                indexOptions,
                new SubmitOptions(request.PodcastId, request.MatchOtherServices, !request.DryRun,
                    request.CreatePodcast));
            logger.LogInformation(result.ToString());
            if (result.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                updatedEpisodeIds.Add(result.EpisodeId!.Value);
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
                logger.LogError("Failure indexing changes.");
            }
        }
    }
}