using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.JsonSplitCosmosDbUploader;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace JsonSplitCosmosDbUploader;

public class JsonSplitCosmosDbUploadProcessor(
    IFileRepository fileRepository,
    IPodcastRepository podcastRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IPodcastFactory podcastFactory,
    ILogger<JsonSplitCosmosDbUploadProcessor> logger)
{
    public async Task Run(JsonSplitCosmosDbUploadRequest request)
    {
        var sourcePodcast = await fileRepository.Read<Podcast>(Path.GetFileNameWithoutExtension(request.FileName));
        if (sourcePodcast != null)
        {
            logger.LogInformation($"'{sourcePodcast.Episodes.Count}' episodes.");
            var jsonSerializerOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();
            var json = JsonSerializer.SerializeToUtf8Bytes(sourcePodcast, jsonSerializerOptions);
            logger.LogInformation($"'{json.Length}' utf-8 bytes.");
            int splitFiles = Convert.ToInt16(Math.Ceiling(json.Length / 2000000f));
            logger.LogInformation($"Split into '{splitFiles}' files.");
            int episodesPerFile =
                Convert.ToInt16(Math.Ceiling(sourcePodcast.Episodes.Count / Convert.ToDouble(splitFiles)));
            logger.LogInformation($"Episodes per file '{episodesPerFile}'.");

            for (var i = 0; i < splitFiles; i++)
            {
                var podcast = await podcastFactory.Create(sourcePodcast.Name);
                podcast.FileKey = $"{podcast.FileKey}_{i}";
                podcast.AppleId = sourcePodcast.AppleId;
                podcast.Bundles = sourcePodcast.Bundles;
                podcast.DescriptionRegex = sourcePodcast.DescriptionRegex;
                podcast.EpisodeIncludeTitleRegex = sourcePodcast.EpisodeIncludeTitleRegex;
                podcast.IndexAllEpisodes = sourcePodcast.IndexAllEpisodes && i == splitFiles - 1;
                podcast.EpisodeMatchRegex = sourcePodcast.EpisodeMatchRegex;
                podcast.PrimaryPostService = sourcePodcast.PrimaryPostService;
                podcast.Publisher = sourcePodcast.Publisher;
                podcast.ReleaseAuthority = sourcePodcast.ReleaseAuthority;
                podcast.SkipEnrichingFromYouTube = sourcePodcast.SkipEnrichingFromYouTube;
                podcast.SpotifyEpisodesQueryIsExpensive = sourcePodcast.SpotifyEpisodesQueryIsExpensive;
                podcast.SpotifyId = sourcePodcast.SpotifyId;
                podcast.SpotifyMarket = sourcePodcast.SpotifyMarket;
                podcast.TitleRegex = sourcePodcast.TitleRegex;
                podcast.TwitterHandle = sourcePodcast.TwitterHandle;
                podcast.YouTubeChannelId = sourcePodcast.YouTubeChannelId;
                podcast.YouTubeNotificationSubscriptionLeaseExpiry = null;
                podcast.YouTubePlaylistId = sourcePodcast.YouTubePlaylistId;
                podcast.YouTubePlaylistQueryIsExpensive = sourcePodcast.YouTubePlaylistQueryIsExpensive;
                podcast.YouTubePublicationOffset = sourcePodcast.YouTubePublicationOffset;
                podcast.Episodes = sourcePodcast.Episodes.Skip((splitFiles - (i + 1)) * episodesPerFile)
                    .Take(episodesPerFile)
                    .OrderByDescending(x => x.Release).ToList();
                await podcastRepository.Save(podcast);
            }
        }
    }
}