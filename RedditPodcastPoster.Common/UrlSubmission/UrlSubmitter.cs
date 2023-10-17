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
        IPodcastRepository podcastRepository,
        IUrlCategoriser urlCategoriser,
        ILogger<UrlSubmitter> logger)
    {
        _podcastRepository = podcastRepository;
        _urlCategoriser = urlCategoriser;
        _logger = logger;
    }

    public async Task Submit(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext, bool searchForPodcast,
        bool matchOtherServices)
    {
        var categorisedItem =
            await _urlCategoriser.Categorise(podcasts, url, indexingContext, searchForPodcast, matchOtherServices);

        if (categorisedItem.MatchingPodcast != null)
        {
            var matchingEpisode = categorisedItem.MatchingEpisode ??
                                  categorisedItem.MatchingPodcast.Episodes.SingleOrDefault(episode =>
                                      IsMatchingEpisode(episode, categorisedItem));

            _logger.LogInformation(
                $"Adding to podcast with name '{categorisedItem.MatchingPodcast.Name}' and id '{categorisedItem.MatchingPodcast.Id}'.");

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
                categorisedItem.MatchingPodcast.Episodes =
                    categorisedItem.MatchingPodcast.Episodes.OrderByDescending(x => x.Release).ToList();
            }

            await _podcastRepository.Save(categorisedItem.MatchingPodcast);
        }
        else
        {
            var newPodcast = CreatePodcastWithEpisode(categorisedItem);

            await _podcastRepository.Save(newPodcast);
            podcasts.Add(newPodcast);
        }
    }

    private Podcast CreatePodcastWithEpisode(CategorisedItem categorisedItem)
    {
        string showName;
        string publisher;
        switch (categorisedItem.Authority)
        {
            case Service.Apple:
                showName = categorisedItem.ResolvedAppleItem.ShowName;
                publisher = categorisedItem.ResolvedAppleItem.Publisher;
                break;
            case Service.Spotify:
                showName = categorisedItem.ResolvedSpotifyItem.ShowName;
                publisher = categorisedItem.ResolvedSpotifyItem.Publisher;
                break;
            case Service.YouTube:
                showName = categorisedItem.ResolvedYouTubeItem.ShowName;
                publisher = categorisedItem.ResolvedYouTubeItem.Publisher;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var newPodcast = new PodcastFactory().Create(showName);
        newPodcast.Publisher = publisher;
        newPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem?.ShowId ?? string.Empty;
        newPodcast.AppleId = categorisedItem.ResolvedAppleItem?.ShowId;
        newPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem?.ShowId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(newPodcast.YouTubeChannelId))
        {
            newPodcast.YouTubePublishingDelayTimeSpan = "0:01:00:00";
        }

        newPodcast.Episodes.Add(CreateEpisode(categorisedItem));
        _logger.LogInformation($"Created podcast with name '{showName}' with id '{newPodcast.Id}'.");

        return newPodcast;
    }

    private Episode CreateEpisode(CategorisedItem categorisedItem)
    {
        string title;
        DateTime release;
        TimeSpan length;
        bool @explicit;
        string description;

        switch (categorisedItem.Authority)
        {
            case Service.Apple:
                title = categorisedItem.ResolvedAppleItem.EpisodeTitle;
                release = categorisedItem.ResolvedAppleItem.Release;
                length = categorisedItem.ResolvedAppleItem.Duration;
                @explicit = categorisedItem.ResolvedAppleItem.Explicit;
                description = categorisedItem.ResolvedAppleItem.EpisodeDescription;
                break;
            case Service.Spotify:
                title = categorisedItem.ResolvedSpotifyItem.EpisodeTitle;
                release = categorisedItem.ResolvedSpotifyItem.Release;
                length = categorisedItem.ResolvedSpotifyItem.Duration;
                @explicit = categorisedItem.ResolvedSpotifyItem.Explicit;
                description = categorisedItem.ResolvedSpotifyItem.EpisodeDescription;
                break;
            case Service.YouTube:
                title = categorisedItem.ResolvedYouTubeItem.EpisodeTitle;
                release = categorisedItem.ResolvedYouTubeItem.Release;
                length = categorisedItem.ResolvedYouTubeItem.Duration;
                @explicit = categorisedItem.ResolvedYouTubeItem.Explicit;
                description = categorisedItem.ResolvedYouTubeItem.EpisodeDescription;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var newEpisode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = title,
            Release = release,
            Length = length,
            Explicit = @explicit,
            AppleId = categorisedItem.ResolvedAppleItem?.EpisodeId,
            SpotifyId = categorisedItem.ResolvedSpotifyItem?.EpisodeId ?? string.Empty,
            YouTubeId = categorisedItem.ResolvedYouTubeItem?.EpisodeId ?? string.Empty,
            Description = description,
            Urls = new ServiceUrls
            {
                Spotify = categorisedItem.ResolvedSpotifyItem?.Url,
                Apple = categorisedItem.ResolvedAppleItem?.Url,
                YouTube = categorisedItem.ResolvedYouTubeItem?.Url
            }
        };
        _logger.LogInformation(
            $"Created episode with spotify-id '{categorisedItem.ResolvedSpotifyItem?.EpisodeId}', apple-id '{categorisedItem.ResolvedAppleItem?.EpisodeId}', youtube-id '{categorisedItem.ResolvedYouTubeItem?.EpisodeId}'.");
        return newEpisode;
    }

    private void ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        Episode matchingEpisode,
        CategorisedItem categorisedItem)
    {
        _logger.LogInformation(
            $"Applying to episode with title '{matchingEpisode.Title}' and id '{matchingEpisode.Id}'.");

        if (categorisedItem.ResolvedAppleItem != null)
        {
            if (!matchingPodcast.AppleId.HasValue)
            {
                matchingPodcast.AppleId = categorisedItem.ResolvedAppleItem.ShowId;
                _logger.LogInformation(
                    $"Enriched podcast with apple details with apple-id {categorisedItem.ResolvedAppleItem.ShowId}.");
            }

            if (!matchingEpisode.AppleId.HasValue)
            {
                matchingEpisode.AppleId = categorisedItem.ResolvedAppleItem.EpisodeId;
                _logger.LogInformation(
                    $"Enriched episode with apple details with apple-id {categorisedItem.ResolvedAppleItem.EpisodeId}.");
            }

            if (matchingEpisode.Urls.Apple == null)
            {
                matchingEpisode.Urls.Apple = categorisedItem.ResolvedAppleItem.Url;
                _logger.LogInformation(
                    $"Enriched episode with apple details with apple-url {categorisedItem.ResolvedAppleItem.Url}.");
            }
        }

        if (categorisedItem.ResolvedSpotifyItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.SpotifyId))
            {
                matchingPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem.ShowId;
                _logger.LogInformation(
                    $"Enriched podcast with spotify details with spotify-id {categorisedItem.ResolvedSpotifyItem.ShowId}.");
            }

            if (string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId))
            {
                matchingEpisode.SpotifyId = categorisedItem.ResolvedSpotifyItem.EpisodeId;
                _logger.LogInformation(
                    $"Enriched episode with spotify details with spotify-id {categorisedItem.ResolvedSpotifyItem.EpisodeId}.");
            }

            if (matchingEpisode.Urls.Spotify == null)
            {
                matchingEpisode.Urls.Spotify = categorisedItem.ResolvedSpotifyItem.Url;
                _logger.LogInformation(
                    $"Enriched episode with spotify details with spotify-url {categorisedItem.ResolvedSpotifyItem.Url}.");
            }
        }

        if (categorisedItem.ResolvedYouTubeItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.YouTubeChannelId))
            {
                matchingPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem.ShowId;
                _logger.LogInformation(
                    $"Enriched podcast with youtube details with youtube-id {categorisedItem.ResolvedYouTubeItem.ShowId}.");
            }

            if (string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId))
            {
                matchingEpisode.YouTubeId = categorisedItem.ResolvedYouTubeItem.EpisodeId;
                _logger.LogInformation(
                    $"Enriched episode with youtube details with youtube-id {categorisedItem.ResolvedYouTubeItem.EpisodeId}.");
            }

            if (matchingEpisode.Urls.YouTube == null)
            {
                matchingEpisode.Urls.YouTube = categorisedItem.ResolvedYouTubeItem.Url;
                _logger.LogInformation(
                    $"Enriched episode with youtube details with youtube-url {categorisedItem.ResolvedYouTubeItem.Url}.");
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