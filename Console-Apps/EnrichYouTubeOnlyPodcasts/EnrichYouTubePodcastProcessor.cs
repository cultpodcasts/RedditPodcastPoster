using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
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
using RedditPodcastPoster.Text.EliminationTerms;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastProcessor(
    IYouTubeServiceWrapper youTubeService,
    IPodcastRepository podcastRepository,
    IYouTubePlaylistService youTubePlaylistService,
    IYouTubeChannelService youTubeChannelService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    ISubjectEnricher subjectEnricher,
    IEliminationTermsProvider eliminationTermsProvider,
    IPodcastFilter podcastFilter,
    IOptions<PostingCriteria> postingCriteria,
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

        var podcast = await podcastRepository.GetPodcast(request.PodcastGuid);

        if (podcast == null)
        {
            logger.LogError($"Podcast with id '{request.PodcastGuid}' not found.");
            return;
        }

        if (podcast.YouTubePlaylistQueryIsExpensive.HasValue && podcast.YouTubePlaylistQueryIsExpensive.Value &&
            !request.AcknowledgeExpensiveYouTubePlaylistQuery)
        {
            logger.LogError($"Query for playlist '{podcast.YouTubePlaylistId}' is expensive.");
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
                await youTubeChannelService.GetChannel(new YouTubeChannelId(podcast.YouTubeChannelId),
                    indexOptions);
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
            await youTubePlaylistService.GetPlaylistVideoSnippets(new YouTubePlaylistId(playlistId),
                indexOptions);
        if (playlistQueryResponse.Result == null)
        {
            logger.LogError($"Unable to retrieve playlist items from playlist '{playlistId}'.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.PlaylistId) &&
            playlistQueryResponse.IsExpensiveQuery && !request.AcknowledgeExpensiveYouTubePlaylistQuery)
        {
            logger.LogError($"Querying '{playlistId}' is noted for being an expensive query.");
            podcast.YouTubePlaylistQueryIsExpensive = true;
        }

        var missingPlaylistItems = playlistQueryResponse.Result.Where(playlistItem =>
            podcast.Episodes.All(episode => !Matches(episode, playlistItem, episodeMatchRegex))).ToList();
        var missingVideoIds = missingPlaylistItems.Select(x => x.Snippet.ResourceId.VideoId).Distinct();
        var missingPlaylistVideos =
            await youTubeVideoService.GetVideoContentDetails(youTubeService, missingVideoIds, indexOptions, true);

        if (missingPlaylistVideos == null)
        {
            logger.LogError($"Unable to retrieve details of videos with ids {string.Join(",", missingVideoIds)}.");
            return;
        }

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
                    episode.Ignored = !((podcast.BypassShortEpisodeChecking.HasValue &&
                                         podcast.BypassShortEpisodeChecking.Value) ||
                                        episode.Length > _postingCriteria.MinimumDuration);
                    var results = await subjectEnricher.EnrichSubjects(episode,
                        new SubjectEnrichmentOptions(
                            podcast.IgnoredAssociatedSubjects,
                            podcast.IgnoredSubjects,
                            podcast.DefaultSubject));

                    podcast.Episodes.Add(episode);
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

        await podcastRepository.Save(podcast);
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