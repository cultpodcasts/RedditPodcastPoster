using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlSubmission;

public class UrlSubmitter : IUrlSubmitter
{
    private readonly ILogger<UrlSubmitter> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IUrlCategoriser _urlCategoriser;

    public UrlSubmitter(
        IUrlCategoriser urlCategoriser,
        IPodcastRepository podcastRepository,
        ILogger<UrlSubmitter> logger)
    {
        _urlCategoriser = urlCategoriser;
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task Submit(Uri url, IndexOptions indexOptions)
    {
        var categorisedItem = await _urlCategoriser.Categorise(url, indexOptions);

        if (categorisedItem.MatchingPodcast != null)
        {
            var matchingEpisode = categorisedItem.MatchingEpisode ??
                                  categorisedItem.MatchingPodcast.Episodes.SingleOrDefault(episode =>
                                      IsMatchingEpisode(episode, categorisedItem));

            if (matchingEpisode != null)
            {
                ApplyResolvedPodcastServiceProperties(
                    categorisedItem.MatchingPodcast, 
                    matchingEpisode,
                    categorisedItem);
            }
            else
            {
                categorisedItem.MatchingPodcast.Episodes.Add(CreateEpisode(categorisedItem));
            }

            await _podcastRepository.Save(categorisedItem.MatchingPodcast);
        }
        else
        {
            var newPodcast = CreatePodcastWithEpisode(categorisedItem);

            await _podcastRepository.Save(newPodcast);
        }
    }

    private static Podcast CreatePodcastWithEpisode(CategorisedItem categorisedItem)
    {
        var newPodcast = new PodcastFactory().Create(categorisedItem.ResolvedSpotifyItem?.ShowName ??
                                                     categorisedItem.ResolvedAppleItem?.ShowName ??
                                                     categorisedItem.ResolvedYouTubeItem?.ShowName ??
                                                     string.Empty);
        newPodcast.Publisher = categorisedItem.ResolvedSpotifyItem?.Publisher ??
                               categorisedItem.ResolvedAppleItem?.Publisher ??
                               categorisedItem.ResolvedYouTubeItem?.Publisher ??
                               string.Empty;
        newPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem?.ShowId ?? string.Empty;
        newPodcast.AppleId = categorisedItem.ResolvedAppleItem?.ShowId;
        newPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem?.ShowId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(newPodcast.YouTubeChannelId))
        {
            newPodcast.YouTubePublishingDelayTimeSpan = "1:00:00:00";
        }

        newPodcast.Episodes.Add(CreateEpisode(categorisedItem));
        return newPodcast;
    }

    private static Episode CreateEpisode(CategorisedItem categorisedItem)
    {
        var newEpisode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = categorisedItem.ResolvedSpotifyItem?.EpisodeTitle ??
                    categorisedItem.ResolvedAppleItem?.EpisodeTitle ??
                    categorisedItem.ResolvedYouTubeItem?.EpisodeTitle ??
                    string.Empty,
            Release = categorisedItem.ResolvedSpotifyItem?.Release ??
                      categorisedItem.ResolvedAppleItem?.Release ??
                      categorisedItem.ResolvedYouTubeItem?.Release ??
                      DateTime.MinValue,
            Length = categorisedItem.ResolvedSpotifyItem?.Duration ??
                     categorisedItem.ResolvedAppleItem?.Duration ??
                     categorisedItem.ResolvedYouTubeItem?.Duration ??
                     TimeSpan.MinValue,
            Explicit = categorisedItem.ResolvedSpotifyItem?.Explicit ??
                       categorisedItem.ResolvedAppleItem?.Explicit ??
                       categorisedItem.ResolvedYouTubeItem?.Explicit ??
                       false,
            AppleId = categorisedItem.ResolvedAppleItem?.EpisodeId,
            SpotifyId = categorisedItem.ResolvedSpotifyItem?.EpisodeId ?? string.Empty,
            YouTubeId = categorisedItem.ResolvedYouTubeItem?.EpisodeId ?? string.Empty,
            Description = categorisedItem.ResolvedSpotifyItem?.EpisodeDescription ??
                          categorisedItem.ResolvedAppleItem?.EpisodeDescription ??
                          categorisedItem.ResolvedYouTubeItem?.EpisodeDescription ??
                          string.Empty,
            Urls = new ServiceUrls
            {
                Spotify = categorisedItem.ResolvedSpotifyItem?.Url,
                Apple = categorisedItem.ResolvedAppleItem?.Url,
                YouTube = categorisedItem.ResolvedYouTubeItem?.Url
            }
        };
        return newEpisode;
    }

    private static void ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        Episode matchingEpisode,
        CategorisedItem categorisedItem)
    {
        if (categorisedItem.ResolvedAppleItem != null)
        {
            if (!matchingPodcast.AppleId.HasValue)
            {
                matchingPodcast.AppleId = categorisedItem.ResolvedAppleItem.ShowId;
            }

            if (!matchingEpisode.AppleId.HasValue)
            {
                matchingEpisode.AppleId = categorisedItem.ResolvedAppleItem.EpisodeId;
            }

            if (matchingEpisode.Urls.Apple == null)
            {
                matchingEpisode.Urls.Apple = categorisedItem.ResolvedAppleItem.Url;
            }
        }

        if (categorisedItem.ResolvedSpotifyItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.SpotifyId))
            {
                matchingPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem.ShowId;
            }

            if (string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId))
            {
                matchingEpisode.SpotifyId = categorisedItem.ResolvedSpotifyItem.EpisodeId;
            }

            if (matchingEpisode.Urls.Spotify == null)
            {
                matchingEpisode.Urls.Spotify = categorisedItem.ResolvedSpotifyItem.Url;
            }
        }

        if (categorisedItem.ResolvedYouTubeItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.YouTubeChannelId))
            {
                matchingPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem.ShowId;
            }

            if (string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId))
            {
                matchingEpisode.YouTubeId = categorisedItem.ResolvedYouTubeItem.EpisodeId;
            }

            if (matchingEpisode.Urls.YouTube == null)
            {
                matchingEpisode.Urls.YouTube = categorisedItem.ResolvedYouTubeItem.Url;
            }
        }
    }

    private bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem)
    {
        if (categorisedItem.ResolvedAppleItem != null &&
            categorisedItem.ResolvedAppleItem.EpisodeTitle == episode.Title)
        {
            return true;
        }

        if (categorisedItem.ResolvedSpotifyItem != null &&
            categorisedItem.ResolvedSpotifyItem.EpisodeTitle == episode.Title)
        {
            return true;
        }

        if (categorisedItem.ResolvedYouTubeItem != null &&
            categorisedItem.ResolvedYouTubeItem.EpisodeTitle == episode.Title)
        {
            return true;
        }

        return false;
    }
}