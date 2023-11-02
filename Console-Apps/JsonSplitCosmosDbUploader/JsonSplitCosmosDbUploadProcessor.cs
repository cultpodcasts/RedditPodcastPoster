using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.JsonSplitCosmosDbUploader;

public class JsonSplitCosmosDbUploadProcessor
{
    private readonly IFileRepository _fileRepository;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger<JsonSplitCosmosDbUploadProcessor> _logger;
    private readonly PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _podcastRepository;

    public JsonSplitCosmosDbUploadProcessor(
        IFileRepository fileRepository,
        IPodcastRepository podcastRepository,
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        PodcastFactory podcastFactory,
        ILogger<JsonSplitCosmosDbUploadProcessor> logger)
    {
        _fileRepository = fileRepository;
        _podcastRepository = podcastRepository;
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider;
        _podcastFactory = podcastFactory;
        _logger = logger;
    }

    public async Task Run(JsonSplitCosmosDbUploadRequest request)
    {
        var sourcePodcast = await _fileRepository.Read<Podcast>(Path.GetFileNameWithoutExtension(request.FileName),
            Podcast.PartitionKey);
        _logger.LogInformation($"'{sourcePodcast.Episodes.Count}' episodes.");
        var jsonSerializerOptions = _jsonSerializerOptionsProvider.GetJsonSerializerOptions();
        var json = JsonSerializer.SerializeToUtf8Bytes(sourcePodcast, jsonSerializerOptions);
        _logger.LogInformation($"'{json.Length}' utf-8 bytes.");
        int splitFiles = Convert.ToInt16(Math.Ceiling(json.Length / 2000000f));
        _logger.LogInformation($"Split into '{splitFiles}' files.");
        int episodesPerFile =
            Convert.ToInt16(Math.Ceiling(sourcePodcast.Episodes.Count / Convert.ToDouble(splitFiles)));
        _logger.LogInformation($"Episodes per file '{episodesPerFile}'.");

        for (var i = 0; i < splitFiles; i++)
        {
            var podcast = _podcastFactory.Create(sourcePodcast.Name);
            podcast.FileKey = $"{podcast.Name}_{i}";
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
            podcast.YouTubePublishingDelayTimeSpan = sourcePodcast.YouTubePublishingDelayTimeSpan;
            podcast.Episodes = sourcePodcast.Episodes.Skip((splitFiles - (i + 1)) * episodesPerFile)
                .Take(episodesPerFile)
                .OrderByDescending(x => x.Release).ToList();
            await _podcastRepository.Save(podcast);
        }
    }
}