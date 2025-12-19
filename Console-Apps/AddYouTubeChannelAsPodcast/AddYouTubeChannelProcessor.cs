using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace AddYouTubeChannelAsPodcast;

public class AddYouTubeChannelProcessor(
    IYouTubeChannelResolver youTubeChannelResolver,
    IYouTubeChannelService youTubeChannelService,
    PodcastFactory podcastFactory,
    IPodcastRepository repository,
    ILogger<AddYouTubeChannelProcessor> logger)
{
    public async Task<bool> Run(Args request)
    {
        var indexOptions = new IndexingContext();
        var match = await youTubeChannelResolver.FindChannelsSnippets(
            request.ChannelName,
            request.MostRecentUploadedVideoTitle,
            indexOptions);
        if (match != null)
        {
            logger.LogInformation("Found channel-id: {SnippetChannelId}", match.Snippet.ChannelId);
            var matchingPodcast = await repository.GetBy(x => x.YouTubeChannelId == match.Snippet.ChannelId);
            if (matchingPodcast != null)
            {
                logger.LogError(
                    "Found existing podcast with YouTube-Id '{SnippetChannelId}' with Podcast-id '{MatchingPodcastId}'.", match.Snippet.ChannelId, matchingPodcast.Id);
                return false;
            }

            var channel =
                await youTubeChannelService.GetChannel(new YouTubeChannelId(match.Snippet.ChannelId),
                    indexOptions, withContentOwnerDetails: true);
            var newPodcast = await podcastFactory.Create(match.Snippet.ChannelTitle);
            newPodcast.Publisher = channel?.ContentOwnerDetails.ContentOwner ?? string.Empty;
            newPodcast.YouTubePublicationOffset = null;
            newPodcast.YouTubeChannelId = match.Snippet.ChannelId;
            newPodcast.ReleaseAuthority = Service.YouTube;
            newPodcast.PrimaryPostService = Service.YouTube;
            await repository.Save(newPodcast);
            logger.LogInformation("Created podcast with name '{NewPodcastName}' and id '{NewPodcastId}'.", newPodcast.Name, newPodcast.Id);
            return true;
        }

        logger.LogError("Failed to match channel");
        return false;
    }
}