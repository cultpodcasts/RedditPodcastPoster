﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class UrlSubmitter : IUrlSubmitter
{
    private readonly ILogger<UrlSubmitter> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly PostingCriteria _postingCriteria;
    private readonly ISubjectEnricher _subjectEnricher;
    private readonly IUrlCategoriser _urlCategoriser;

    public UrlSubmitter(
        IPodcastRepository podcastRepository,
        IUrlCategoriser urlCategoriser,
        ISubjectEnricher subjectEnricher,
        IOptions<PostingCriteria> postingCriteria,
        ILogger<UrlSubmitter> logger)
    {
        _podcastRepository = podcastRepository;
        _urlCategoriser = urlCategoriser;
        _subjectEnricher = subjectEnricher;
        _postingCriteria = postingCriteria.Value;
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
                $"Modifying podcast with name '{categorisedItem.MatchingPodcast.Name}' and id '{categorisedItem.MatchingPodcast.Id}'.");


            ApplyResolvedPodcastServiceProperties(
                categorisedItem.MatchingPodcast,
                categorisedItem, matchingEpisode);

            if (matchingEpisode == null)
            {
                var episode = CreateEpisode(categorisedItem);
                await _subjectEnricher.EnrichSubjects(
                    episode,
                    new SubjectEnrichmentOptions(
                        categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                        categorisedItem.MatchingPodcast.DefaultSubject));
                categorisedItem.MatchingPodcast.Episodes.Add(episode);
                categorisedItem.MatchingPodcast.Episodes =
                    categorisedItem.MatchingPodcast.Episodes.OrderByDescending(x => x.Release).ToList();
            }

            await _podcastRepository.Save(categorisedItem.MatchingPodcast);
        }
        else
        {
            var newPodcast = await CreatePodcastWithEpisode(categorisedItem);

            await _podcastRepository.Save(newPodcast);
            podcasts.Add(newPodcast);
        }
    }

    private async Task<Podcast> CreatePodcastWithEpisode(CategorisedItem categorisedItem)
    {
        string showName;
        string publisher;
        switch (categorisedItem.Authority)
        {
            case Service.Apple:
                showName = categorisedItem.ResolvedAppleItem!.ShowName;
                publisher = categorisedItem.ResolvedAppleItem.Publisher;
                break;
            case Service.Spotify:
                showName = categorisedItem.ResolvedSpotifyItem!.ShowName;
                publisher = categorisedItem.ResolvedSpotifyItem.Publisher;
                break;
            case Service.YouTube:
                showName = categorisedItem.ResolvedYouTubeItem!.ShowName;
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

        var episode = CreateEpisode(categorisedItem);
        await _subjectEnricher.EnrichSubjects(episode);
        newPodcast.Episodes.Add(episode);
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
                title = categorisedItem.ResolvedAppleItem!.EpisodeTitle;
                release = categorisedItem.ResolvedAppleItem.Release;
                length = categorisedItem.ResolvedAppleItem.Duration;
                @explicit = categorisedItem.ResolvedAppleItem.Explicit;
                description = categorisedItem.ResolvedAppleItem.EpisodeDescription;
                break;
            case Service.Spotify:
                title = categorisedItem.ResolvedSpotifyItem!.EpisodeTitle;
                release =
                    categorisedItem.ResolvedSpotifyItem.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedAppleItem != null
                        ? categorisedItem.ResolvedAppleItem.Release
                        : categorisedItem.ResolvedSpotifyItem.Release;
                length = categorisedItem.ResolvedSpotifyItem.Duration;
                @explicit = categorisedItem.ResolvedSpotifyItem.Explicit;
                description = categorisedItem.ResolvedSpotifyItem.EpisodeDescription;
                break;
            case Service.YouTube:
                title = categorisedItem.ResolvedYouTubeItem!.EpisodeTitle;
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
        if (categorisedItem.MatchingPodcast != null)
        {
            if (categorisedItem.MatchingPodcast.BypassShortEpisodeChecking.HasValue &&
                categorisedItem.MatchingPodcast.BypassShortEpisodeChecking.Value)
            {
                newEpisode.Ignored = false;
            }
            else
            {
                newEpisode.Ignored = length < _postingCriteria.MinimumDuration;
            }
        }
        else
        {
            newEpisode.Ignored = length < _postingCriteria.MinimumDuration;
        }

        _logger.LogInformation(
            $"Created episode with spotify-id '{categorisedItem.ResolvedSpotifyItem?.EpisodeId}', apple-id '{categorisedItem.ResolvedAppleItem?.EpisodeId}', youtube-id '{categorisedItem.ResolvedYouTubeItem?.EpisodeId}' and episode-id '{newEpisode.Id}'.");
        return newEpisode;
    }

    private void ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        CategorisedItem categorisedItem,
        Episode? matchingEpisode)
    {
        if (matchingEpisode != null)
        {
            _logger.LogInformation(
                $"Applying to episode with title '{matchingEpisode.Title}' and id '{matchingEpisode.Id}'.");
        }

        if (categorisedItem.ResolvedAppleItem != null)
        {
            if (!matchingPodcast.AppleId.HasValue)
            {
                matchingPodcast.AppleId = categorisedItem.ResolvedAppleItem.ShowId;
                _logger.LogInformation(
                    $"Enriched podcast with apple details with apple-id {categorisedItem.ResolvedAppleItem.ShowId}.");
            }

            if (matchingEpisode != null)
            {
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
        }

        if (categorisedItem.ResolvedSpotifyItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.SpotifyId))
            {
                matchingPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem.ShowId;
                _logger.LogInformation(
                    $"Enriched podcast with spotify details with spotify-id {categorisedItem.ResolvedSpotifyItem.ShowId}.");
            }

            if (matchingEpisode != null)
            {
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
        }

        if (categorisedItem.ResolvedYouTubeItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.YouTubeChannelId))
            {
                matchingPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem.ShowId;
                _logger.LogInformation(
                    $"Enriched podcast with youtube details with youtube-id {categorisedItem.ResolvedYouTubeItem.ShowId}.");
            }

            if (matchingEpisode != null)
            {
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
    }

    private bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem)
    {
        var episodeTitle = episode.Title.Trim();
        if (categorisedItem.ResolvedAppleItem != null &&
            categorisedItem.ResolvedAppleItem.EpisodeTitle.Trim() == episodeTitle)
        {
            return true;
        }

        if (categorisedItem.ResolvedSpotifyItem != null &&
            categorisedItem.ResolvedSpotifyItem.EpisodeTitle.Trim() == episodeTitle)
        {
            return true;
        }

        if (categorisedItem.ResolvedYouTubeItem != null &&
            categorisedItem.ResolvedYouTubeItem.EpisodeTitle.Trim() == episodeTitle)
        {
            return true;
        }

        return false;
    }
}