using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Models;

namespace SubmitUrl;

public class UrlSubmitter
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

    public async Task Run(Uri url)
    {
        var categorisedItem = await _urlCategoriser.Categorise(url);

        var podcasts = await _podcastRepository.GetAll().ToListAsync();
        var matchingPodcast = podcasts.SingleOrDefault(podcast => IsMatchingPodcast(podcast, categorisedItem));
        if (matchingPodcast != null)
        {
            var matchingEpisode =
                matchingPodcast.Episodes.SingleOrDefault(episode => IsMatchingEpisode(episode, categorisedItem));

            if (matchingEpisode != null)
            {
                ApplyResolvedPodcastServiceProperties(matchingEpisode, categorisedItem);
            }
            else
            {
                matchingPodcast.Episodes.Add(CreateEpisode(categorisedItem));
            }

            await _podcastRepository.Save(matchingPodcast);
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

    private static void ApplyResolvedPodcastServiceProperties(Episode matchingEpisode, CategorisedItem categorisedItem)
    {
        if (!matchingEpisode.AppleId.HasValue && categorisedItem.ResolvedAppleItem != null)
        {
            matchingEpisode.AppleId = categorisedItem.ResolvedAppleItem.EpisodeId;
        }

        if (matchingEpisode.Urls.Apple == null && categorisedItem.ResolvedAppleItem != null)
        {
            matchingEpisode.Urls.Apple = categorisedItem.ResolvedAppleItem.Url;
        }

        if (!string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId) && categorisedItem.ResolvedSpotifyItem != null)
        {
            matchingEpisode.SpotifyId = categorisedItem.ResolvedSpotifyItem.EpisodeId;
        }

        if (matchingEpisode.Urls.Spotify == null && categorisedItem.ResolvedSpotifyItem != null)
        {
            matchingEpisode.Urls.Spotify = categorisedItem.ResolvedSpotifyItem.Url;
        }

        if (!string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId) && categorisedItem.ResolvedYouTubeItem != null)
        {
            matchingEpisode.YouTubeId = categorisedItem.ResolvedYouTubeItem.EpisodeId;
        }

        if (matchingEpisode.Urls.YouTube == null && categorisedItem.ResolvedYouTubeItem != null)
        {
            matchingEpisode.Urls.YouTube = categorisedItem.ResolvedYouTubeItem.Url;
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

    private bool IsMatchingPodcast(Podcast podcast, CategorisedItem categorisedItem)
    {
        if (categorisedItem.ResolvedAppleItem != null &&
            podcast.AppleId.HasValue &&
            categorisedItem.ResolvedAppleItem.ShowId.HasValue &&
            categorisedItem.ResolvedAppleItem.ShowId == podcast.AppleId)
        {
            return true;
        }

        if (categorisedItem.ResolvedYouTubeItem != null &&
            !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
            categorisedItem.ResolvedYouTubeItem.ShowId == podcast.YouTubeChannelId)
        {
            return true;
        }

        if (categorisedItem.ResolvedSpotifyItem != null &&
            !string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
            categorisedItem.ResolvedSpotifyItem.ShowId == podcast.SpotifyId)
        {
            return true;
        }

        return false;
    }
}