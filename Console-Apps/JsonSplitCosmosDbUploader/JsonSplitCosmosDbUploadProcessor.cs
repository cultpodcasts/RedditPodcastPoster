using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.JsonSplitCosmosDbUploader;
using RedditPodcastPoster.Persistence.Legacy;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.Episode;
using Podcast = RedditPodcastPoster.Persistence.Legacy.Podcast;

namespace JsonSplitCosmosDbUploader;

public class JsonSplitCosmosDbUploadProcessor(
    IFileRepository fileRepository,
    IPodcastRepositoryV2 podcastRepositoryV2,
    IEpisodeRepository episodeRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IPodcastFactory podcastFactory,
    ILogger<JsonSplitCosmosDbUploadProcessor> logger)
{
    public async Task Run(JsonSplitCosmosDbUploadRequest request)
    {
        var sourcePodcast = await fileRepository.Read<Podcast>(Path.GetFileNameWithoutExtension(request.FileName));
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
                podcast.Language = sourcePodcast.Language;

                await podcastRepositoryV2.Save(podcast);

                var sourceEpisodes = sourcePodcast.Episodes
                    .Skip(i * episodesPerFile)
                    .Take(episodesPerFile)
                    .OrderByDescending(x => x.Release)
                    .ToArray();

                var episodes = sourceEpisodes.Select(episode => new Episode
                {
                    Id = episode.Id,
                    PodcastId = podcast.Id,
                    Title = episode.Title,
                    Description = episode.Description,
                    Release = episode.Release,
                    Length = episode.Length,
                    Explicit = episode.Explicit,
                    Posted = episode.Posted,
                    Tweeted = episode.Tweeted,
                    BlueskyPosted = episode.BlueskyPosted,
                    Ignored = episode.Ignored,
                    Removed = episode.Removed,
                    SpotifyId = episode.SpotifyId,
                    AppleId = episode.AppleId,
                    YouTubeId = episode.YouTubeId,
                    Urls = episode.Urls,
                    Subjects = episode.Subjects,
                    SearchTerms = episode.SearchTerms,
                    PodcastName = podcast.Name,
                    PodcastSearchTerms = podcast.SearchTerms,
                    PodcastLanguage = podcast.Language,
                    Language = episode.Language,
                    PodcastMetadataVersion = null,
                    PodcastRemoved = podcast.Removed,
                    Images = episode.Images,
                    TwitterHandles = episode.TwitterHandles,
                    BlueskyHandles = episode.BlueskyHandles
                }).ToArray();

                if (episodes.Any())
                {
                    await episodeRepository.Save(episodes);
                }
            }
        }
    }
}