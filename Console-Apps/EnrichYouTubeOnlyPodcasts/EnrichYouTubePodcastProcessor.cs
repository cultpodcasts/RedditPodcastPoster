using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.EliminationTerms;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastProcessor(
    IYouTubeServiceWrapper youTubeService,
    IPodcastRepository podcastRepository,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    IYouTubeChannelService youTubeChannelService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    ISubjectEnricher subjectEnricher,
    IEliminationTermsProvider eliminationTermsProvider,
    IPodcastFilter podcastFilter,
    IFileRepository fileRepository,
    IOptions<PostingCriteria> postingCriteria,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<EnrichYouTubePodcastProcessor> logger)
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task Run(EnrichYouTubePodcastRequest request)
    {
        IndexingContext indexOptions;
        logger.LogInformation(_postingCriteria.ToString());
        if (request.ReleasedSince.HasValue)
        {
            indexOptions = new IndexingContext(DateTime.Today.AddDays(-1 * request.ReleasedSince.Value));
        }
        else
        {
            indexOptions = new IndexingContext();
        }

        Guid podcastId;
        if (request.PodcastGuid.HasValue)
        {
            podcastId = request.PodcastGuid.Value;
        }
        else if (request.PodcastName != null)
        {
            var podcastIds = await podcastRepository
                .GetAllBy(x => x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                    x => x.Id).ToListAsync();
            if (podcastIds.Count > 1)
            {
                logger.LogError("Found {podcastIdsCount} podcasts with name '{podcastName}'.", podcastIds.Count,
                    request.PodcastName);
                return;
            }

            if (podcastIds.Count == 0)
            {
                logger.LogError("No podcasts found with name '{podcastName}'.", request.PodcastName);
                return;
            }

            podcastId = podcastIds.First();
        }
        else
        {
            logger.LogError("No podcast specified.");
            return;
        }

        var podcast = await podcastRepository.GetPodcast(podcastId);

        if (podcast == null)
        {
            logger.LogError("Podcast with id '{PodcastGuid}' not found.", request.PodcastGuid);
            return;
        }

        if (podcast.YouTubePlaylistQueryIsExpensive.HasValue && podcast.YouTubePlaylistQueryIsExpensive.Value &&
            !request.AcknowledgeExpensiveYouTubePlaylistQuery)
        {
            logger.LogError("Query for playlist '{podcastYouTubePlaylistId}' is expensive.", podcast.YouTubePlaylistId);
            return;
        }

        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            logger.LogError("Not appropriate to run this app against a podcast without a YouTube channel-id.");
            return;
        }

        Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, RegexOptions.Compiled);
        }

        string playlistId;
        if (string.IsNullOrWhiteSpace(request.PlaylistId))
        {
            var channel =
                await youTubeChannelService.GetChannel(new YouTubeChannelId(podcast.YouTubeChannelId), indexOptions);
            if (channel == null)
            {
                throw new InvalidOperationException(
                    $"Could not find YouTube channel with channel-id '{podcast.YouTubeChannelId}'.");
            }

            playlistId = channel.ContentDetails.RelatedPlaylists.Uploads;
        }
        else
        {
            playlistId = request.PlaylistId;
        }

        var playlistQueryResponse =
            await youTubePlaylistService.GetPlaylistVideoSnippets(new YouTubePlaylistId(playlistId), indexOptions);
        if (playlistQueryResponse.Result == null)
        {
            logger.LogError("Unable to retrieve playlist items from playlist '{playlistId}'.", playlistId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.PlaylistId) &&
            playlistQueryResponse.IsExpensiveQuery && !request.AcknowledgeExpensiveYouTubePlaylistQuery)
        {
            logger.LogError("Querying '{playlistId}' is noted for being an expensive query.", playlistId);
            podcast.YouTubePlaylistQueryIsExpensive = true;
        }

        var missingPlaylistItems = playlistQueryResponse.Result.Where(playlistItem =>
            podcast.Episodes.All(episode => !Matches(episode, playlistItem, episodeMatchRegex))).ToList();
        var missingVideoIds = missingPlaylistItems.Select(x => x.Snippet.ResourceId.VideoId).Distinct();
        var missingPlaylistVideos =
            await youTubeVideoService.GetVideoContentDetails(youTubeService, missingVideoIds, indexOptions, true);

        if (missingPlaylistVideos == null)
        {
            logger.LogError("Unable to retrieve details of videos with ids {missingVideoIds}.",
                string.Join(",", missingVideoIds));
            return;
        }

        List<Guid> updatedEpisodeIds = new();
        foreach (var missingPlaylistItem in missingPlaylistItems)
        {
            var missingPlaylistItemSnippet =
                playlistQueryResponse.Result.SingleOrDefault(x => x.Id == missingPlaylistItem.Id)!.Snippet;
            var video = missingPlaylistVideos.SingleOrDefault(video =>
                video.Id == missingPlaylistItemSnippet.ResourceId.VideoId);
            if (video != null)
            {
                var episode = youTubeEpisodeProvider.GetEpisode(missingPlaylistItemSnippet, video);
                if (request.IncludeShort ||
                    (podcast.BypassShortEpisodeChecking.HasValue && podcast.BypassShortEpisodeChecking.Value) ||
                    episode.Length > _postingCriteria.MinimumDuration)
                {
                    episode.Id = Guid.NewGuid();
                    if (podcast.HasIgnoreAllEpisodes())
                    {
                        episode.Ignored = true;
                    }
                    else
                    {
                        episode.Ignored = !((podcast.BypassShortEpisodeChecking.HasValue &&
                                             podcast.BypassShortEpisodeChecking.Value) ||
                                            episode.Length >= _postingCriteria.MinimumDuration);
                    }

                    var videoImage = video.GetImageUrl();
                    if (videoImage != null)
                    {
                        episode.Images ??= new EpisodeImages();
                        episode.Images.YouTube = videoImage;
                    }

                    var results = await subjectEnricher.EnrichSubjects(episode,
                        new SubjectEnrichmentOptions(
                            podcast.IgnoredAssociatedSubjects,
                            podcast.IgnoredSubjects,
                            podcast.DefaultSubject,
                            podcast.DescriptionRegex));

                    podcast.Episodes.Add(episode);
                    updatedEpisodeIds.Add(episode.Id);
                }
            }
        }

        foreach (var podcastEpisode in podcast.Episodes)
        {
            if (string.IsNullOrWhiteSpace(podcastEpisode.YouTubeId) || podcastEpisode.Urls.YouTube == null)
            {
                var youTubeItems =
                    playlistQueryResponse.Result.Where(x => Matches(podcastEpisode, x, episodeMatchRegex));

                var youTubeItem = youTubeItems.FirstOrDefault();
                if (youTubeItem != null)
                {
                    podcastEpisode.YouTubeId = youTubeItem.Snippet.ResourceId.VideoId;
                    podcastEpisode.Urls.YouTube = youTubeItem.Snippet.ToYouTubeUrl();
                    updatedEpisodeIds.Add(podcastEpisode.Id);
                }
            }
        }

        podcast.Episodes = podcast.Episodes.OrderByDescending(x => x.Release).ToList();

        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(podcast, eliminationTerms.Terms);
        if (filterResult.FilteredEpisodes.Any())
        {
            logger.LogWarning(filterResult.ToString());
        }

        try
        {
            await podcastRepository.Save(podcast);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write entity to cosmos.");
            try
            {
                await fileRepository.Write(podcast);
                logger.LogInformation("Writing to file '{filename}'.", podcast.FileKey);
            }
            catch (Exception ex2)
            {
                logger.LogError(ex2, "Failed to write to file. Filekey '{filekey}'.", podcast.FileKey);
            }
        }

        await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
    }

    private static bool Matches(Episode episode, PlaylistItem playlistItem, Regex? episodeMatchRegex)
    {
        if (episode.Title.Trim() == playlistItem.Snippet.Title.Trim())
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(episode.YouTubeId) &&
            episode.YouTubeId == playlistItem.Snippet.ResourceId.VideoId)
        {
            return true;
        }

        if (episodeMatchRegex != null)
        {
            var playlistItemMatch = episodeMatchRegex.Match(playlistItem.Snippet.Title);
            var episodeMatch = episodeMatchRegex.Match(episode.Title);
            if (playlistItemMatch.Success && episodeMatch.Success)
            {
                if (playlistItemMatch.Groups["episodematch"].Value ==
                    episodeMatch.Groups["episodematch"].Value)
                {
                    return true;
                }

                if (playlistItemMatch.Groups["title"].Value ==
                    episodeMatch.Groups["title"].Value)
                {
                    return true;
                }
            }
        }

        return false;
    }
}