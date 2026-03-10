using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.JsonSplitCosmosDbUploader;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;

namespace JsonSplitCosmosDbUploader;

public class JsonSplitCosmosDbUploadProcessor(
    IFileRepository fileRepository,
    IPodcastRepository podcastRepository,
    IPodcastRepositoryV2 podcastRepositoryV2,
    IEpisodeRepository episodeRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IPodcastFactory podcastFactory,
    ILogger<JsonSplitCosmosDbUploadProcessor> logger)
{
    public async Task Run(JsonSplitCosmosDbUploadRequest request)
    {
        throw new NotImplementedException("Not implemented during migration");
        var sourcePodcast = await fileRepository.Read<RedditPodcastPoster.Models.Podcast>(Path.GetFileNameWithoutExtension(request.FileName));
        if (sourcePodcast != null)
        {
            logger.LogInformation("'{EpisodesCount}' episodes.", sourcePodcast.Episodes.Count);
            var jsonSerializerOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();
            var json = JsonSerializer.SerializeToUtf8Bytes(sourcePodcast, jsonSerializerOptions);
            logger.LogInformation("'{JsonLength}' utf-8 bytes.", json.Length);
            int splitFiles = Convert.ToInt16(Math.Ceiling(json.Length / 2000000f));
            logger.LogInformation("Split into '{SplitFiles}' files.", splitFiles);
            int episodesPerFile =
                Convert.ToInt16(Math.Ceiling(sourcePodcast.Episodes.Count / Convert.ToDouble(splitFiles)));
            logger.LogInformation("Episodes per file '{EpisodesPerFile}'.", episodesPerFile);

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

                await podcastRepositoryV2.Save(podcast);

                //var episodes= sourcePodcast.Episodes.Select(x=>new RedditPodcastPoster.Models.V2.Episode()
                //{

                //})
                //var episodes = sourcePodcast.Episodes.Skip((splitFiles - (i + 1)) * episodesPerFile)
                //    .Take(episodesPerFile)
                //    .OrderByDescending(x => x.Release).ToList();

            }
        }
    }
}