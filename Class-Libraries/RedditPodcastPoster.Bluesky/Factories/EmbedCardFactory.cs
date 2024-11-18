using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.Bluesky.Factories;

public class EmbedCardFactory(
    IYouTubeVideoService youTubeVideoService,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EmbedCardFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IEmbedCardFactory
{
    public async Task<EmbedCardRequest?> EmbedCardRequest(PodcastEpisode podcastEpisode, BlueskyEmbedCardPost embedPost)
    {
        EmbedCardRequest? embedCardRequest = null;
        switch (embedPost.UrlService)
        {
            case Service.YouTube:
            {
                var video = await youTubeVideoService.GetVideoContentDetails(
                    [podcastEpisode.Episode.YouTubeId],
                    new IndexingContext(), true);
                if (video?.FirstOrDefault() != null)
                {
                    embedCardRequest = new EmbedCardRequest(
                        video.First().Snippet.Title.Trim(),
                        video.First().Snippet.Description.Trim(),
                        embedPost.Url)
                    {
                        ThumbUrl = video.FirstOrDefault().GetImageUrl()
                    };
                }

                break;
            }
            case Service.Spotify:
            {
                var episode = await spotifyEpisodeResolver.FindEpisode(
                    FindSpotifyEpisodeRequestFactory.Create(podcastEpisode.Podcast, podcastEpisode.Episode),
                    new IndexingContext());
                if (episode.FullEpisode != null)
                {
                    embedCardRequest = new EmbedCardRequest(
                        episode.FullEpisode.Name,
                        episode.FullEpisode.Description,
                        embedPost.Url);
                    var maxImage = episode.FullEpisode.Images.MaxBy(x => x.Height);
                    if (maxImage != null)
                    {
                        embedCardRequest.ThumbUrl = new Uri(maxImage.Url);
                    }
                }

                break;
            }
        }

        return embedCardRequest;
    }
}