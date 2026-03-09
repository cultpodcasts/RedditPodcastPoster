using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.DependencyInjection;
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
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastProcessor(
    IYouTubeServiceWrapper youTubeService,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    IYouTubeChannelService youTubeChannelService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    ISubjectEnricher subjectEnricher,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    IPodcastFilter podcastFilter,
    IFileRepository fileRepository,
    IOptions<PostingCriteria> postingCriteria,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    IFoundEpisodeFilter foundEpisodeFilter,
    ILogger<EnrichYouTubePodcastProcessor> logger)
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task Run(EnrichYouTubePodcastRequest request)
    {
        logger.LogInformation(_postingCriteria.ToString());
        var indexOptions = request.ReleasedSince.HasValue
            ? new IndexingContext(DateTime.Today.AddDays(-1 * request.ReleasedSince.Value))
            : new IndexingContext();

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
                logger.LogError("Found {podcastIdsCount} podcasts with name '{podcastName}'. Ids: {ids}.",
                    podcastIds.Count, request.PodcastName, string.Join(",", podcastIds));
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
            episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, Podcast.EpisodeMatchFlags);
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

        // Get existing episodes from detached repository
        var existingEpisodesV2 = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();
        var existingEpisodes = existingEpisodesV2.Select(ToLegacyEpisode).ToList();

        var missingPlaylistItems = playlistQueryResponse.Result.Where(playlistItem =>
            existingEpisodes.All(episode => !Matches(episode, playlistItem, episodeMatchRegex))).ToList();
        IList<string> missingVideoIds =
            missingPlaylistItems.Select(x => x.Snippet.ResourceId.VideoId).Distinct().ToList();
        var missingPlaylistVideos =
            await youTubeVideoService.GetVideoContentDetails(youTubeService, missingVideoIds, indexOptions, true);

        if (missingPlaylistVideos == null)
        {
            logger.LogError("Unable to retrieve details of videos with ids {missingVideoIds}.",
                string.Join(",", missingVideoIds));
            return;
        }

        IList<Episode> addedEpisodes = new List<Episode>();
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
                    episode.Length >= (podcast.MinimumDuration ?? _postingCriteria.MinimumDuration))
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
                                            episode.Length >= (podcast.MinimumDuration ?? _postingCriteria.MinimumDuration));
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
                    addedEpisodes.Add(episode);
                }
            }
        }

        if (addedEpisodes.Any() && !string.IsNullOrEmpty(podcast.EpisodeIncludeTitleRegex))
        {
            addedEpisodes = foundEpisodeFilter.ReduceEpisodes(podcast, addedEpisodes);
        }

        // Add new episodes to working list
        existingEpisodes.AddRange(addedEpisodes);
        IList<Guid> updatedEpisodeIds = addedEpisodes.Select(x => x.Id).ToList();

        // Update existing episodes that are missing YouTube information
        var episodesToUpdate = new List<Episode>();
        foreach (var podcastEpisode in existingEpisodes)
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
                    episodesToUpdate.Add(podcastEpisode);
                    if (!updatedEpisodeIds.Contains(podcastEpisode.Id))
                    {
                        updatedEpisodeIds.Add(podcastEpisode.Id);
                    }
                }
            }
        }

        // Create a temporary podcast with episodes for filtering
        var tempPodcast = new Podcast(podcast.Id)
        {
            Name = podcast.Name,
            Episodes = existingEpisodes.OrderByDescending(x => x.Release).ToList()
        };

        var eliminationTermsProvider = await eliminationTermsProviderInstance.GetAsync();
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(tempPodcast, eliminationTerms.Terms);
        if (filterResult.FilteredEpisodes.Any())
        {
            logger.LogWarning(filterResult.ToString());
        }

        if (updatedEpisodeIds.Any())
        {
            try
            {
                // Convert and save all new and updated episodes to detached repository
                var episodesToSave = new List<V2Episode>();
                
                // Add newly added episodes
                episodesToSave.AddRange(addedEpisodes.Select(e => ToV2Episode(podcast, e)));
                
                // Add updated existing episodes
                episodesToSave.AddRange(episodesToUpdate.Select(e => ToV2Episode(podcast, e)));
                
                if (episodesToSave.Any())
                {
                    await episodeRepository.Save(episodesToSave);
                }

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
                    logger.LogError(ex2, "Failed to write to file. FileKey: '{fileKey}'.", podcast.FileKey);
                }
            }

            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
        else
        {
            logger.LogInformation("No updates made.");
        }
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

    private static Episode ToLegacyEpisode(V2Episode v2Episode)
    {
        return new Episode
        {
            Id = v2Episode.Id,
            Title = v2Episode.Title,
            Description = v2Episode.Description,
            Release = v2Episode.Release,
            Length = v2Episode.Length,
            Explicit = v2Episode.Explicit,
            Posted = v2Episode.Posted,
            Tweeted = v2Episode.Tweeted,
            BlueskyPosted = v2Episode.BlueskyPosted,
            Ignored = v2Episode.Ignored,
            Removed = v2Episode.Removed,
            SpotifyId = v2Episode.SpotifyId,
            AppleId = v2Episode.AppleId,
            YouTubeId = v2Episode.YouTubeId,
            Urls = v2Episode.Urls,
            Subjects = v2Episode.Subjects,
            SearchTerms = v2Episode.SearchTerms,
            Language = v2Episode.SearchLanguage,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }

    private static V2Episode ToV2Episode(Podcast podcast, Episode episode)
    {
        return new V2Episode
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
            Subjects = episode.Subjects ?? [],
            SearchTerms = episode.SearchTerms,
            PodcastName = podcast.Name,
            PodcastSearchTerms = podcast.SearchTerms,
            SearchLanguage = episode.Language ?? podcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}