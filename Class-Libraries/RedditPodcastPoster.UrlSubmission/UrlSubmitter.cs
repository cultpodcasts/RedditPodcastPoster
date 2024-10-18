using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using static RedditPodcastPoster.UrlSubmission.SubmitResult;

namespace RedditPodcastPoster.UrlSubmission;

public class UrlSubmitter(
    IPodcastRepository podcastRepository,
    IPodcastService podcastService,
    IUrlCategoriser urlCategoriser,
    ISubjectEnricher subjectEnricher,
    IOptions<PostingCriteria> postingCriteria,
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    ILogger<UrlSubmitter> logger)
    : IUrlSubmitter
{
    private const int MinFuzzyTitleMatch = 95;
    private static readonly TimeSpan DefaultMatchingPodcastYouTubePublishingDelay = TimeSpan.FromHours(1);
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var episodeResult = SubmitResultState.None;
        SubmitResultState podcastResult;
        try
        {
            Podcast? podcast;
            if (submitOptions.PodcastId != null)
            {
                podcast = await podcastRepository.GetPodcast(submitOptions.PodcastId.Value);
            }
            else
            {
                podcast = await podcastService.GetPodcastFromEpisodeUrl(url, indexingContext);
            }

            if (podcast != null && podcast.IsRemoved())
            {
                logger.LogWarning($"Podcast with id '{podcast.Id}' is removed.");
                return new SubmitResult(episodeResult, SubmitResultState.PodcastRemoved);
            }

            var categorisedItem =
                await urlCategoriser.Categorise(podcast, url, indexingContext, submitOptions.MatchOtherServices);

            var submitResult = await ProcessCategorisedItem(categorisedItem, submitOptions);

            return submitResult;
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Error ingesting '{url}'.");
            return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
        }
    }

    public async Task<DiscoverySubmitResult> Submit(
        DiscoveryResult discoveryResult,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        if (!discoveryResult.Urls.Any())
        {
            return new DiscoverySubmitResult(DiscoverySubmitResultState.NoUrls);
        }

        Podcast? spotifyPodcast = null, applePodcast = null, youTubePodcast = null;
        if (discoveryResult.Urls.Spotify != null)
        {
            spotifyPodcast =
                await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.Spotify, indexingContext);
        }

        if (discoveryResult.Urls.Apple != null)
        {
            applePodcast = await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.Apple, indexingContext);
        }

        if (discoveryResult.Urls.YouTube != null)
        {
            youTubePodcast =
                await podcastService.GetPodcastFromEpisodeUrl(discoveryResult.Urls.YouTube, indexingContext);
        }

        Podcast?[] podcasts = [spotifyPodcast, applePodcast, youTubePodcast];
        IEnumerable<Podcast> foundPodcasts = podcasts.Where(x => x != null)!;
        var areSame = foundPodcasts.All(x => x.Id == foundPodcasts.First().Id);
        if (!areSame)
        {
            return new DiscoverySubmitResult(DiscoverySubmitResultState.DifferentPodcasts);
        }

        bool enrichSpotify = false, enrichApple = false, enrichYouTube = false;
        CategorisedItem categorisedItem;
        if (discoveryResult.Urls.Spotify != null &&
            (spotifyPodcast != null || (applePodcast == null && youTubePodcast == null)))
        {
            categorisedItem = await urlCategoriser.Categorise(spotifyPodcast, discoveryResult.Urls.Spotify,
                indexingContext, submitOptions.MatchOtherServices);
            if (discoveryResult.Urls.Apple != null)
            {
                enrichApple = true;
            }

            if (discoveryResult.Urls.YouTube != null)
            {
                enrichYouTube = true;
            }
        }
        else if (discoveryResult.Urls.Apple != null &&
                 (applePodcast != null || (spotifyPodcast == null && youTubePodcast == null)))
        {
            categorisedItem = await urlCategoriser.Categorise(applePodcast, discoveryResult.Urls.Apple, indexingContext,
                submitOptions.MatchOtherServices);
            if (discoveryResult.Urls.YouTube != null)
            {
                enrichYouTube = true;
            }

            if (discoveryResult.Urls.Spotify != null)
            {
                enrichSpotify = true;
            }
        }
        else
        {
            categorisedItem = await urlCategoriser.Categorise(youTubePodcast, discoveryResult.Urls.YouTube!,
                indexingContext, submitOptions.MatchOtherServices);
            if (discoveryResult.Urls.Apple != null)
            {
                enrichApple = true;
            }

            if (discoveryResult.Urls.Spotify != null)
            {
                enrichSpotify = true;
            }
        }

        if (enrichSpotify)
        {
            if (categorisedItem.ResolvedSpotifyItem == null ||
                categorisedItem.ResolvedSpotifyItem.EpisodeDescription !=
                SpotifyIdResolver.GetEpisodeId(discoveryResult.Urls.Spotify!))
            {
                categorisedItem = categorisedItem with
                {
                    ResolvedSpotifyItem =
                    await spotifyUrlCategoriser.Resolve(null, discoveryResult.Urls.Spotify!, indexingContext)
                };
            }
        }

        if (enrichApple)
        {
            if (categorisedItem.ResolvedAppleItem == null ||
                categorisedItem.ResolvedAppleItem.ShowId !=
                AppleIdResolver.GetPodcastId(discoveryResult.Urls.Apple!) ||
                categorisedItem.ResolvedAppleItem.EpisodeId !=
                AppleIdResolver.GetEpisodeId(discoveryResult.Urls.Apple!))
            {
                categorisedItem = categorisedItem with
                {
                    ResolvedAppleItem =
                    await appleUrlCategoriser.Resolve(null, discoveryResult.Urls.Apple!, indexingContext)
                };
            }
        }

        if (enrichYouTube)
        {
            if (categorisedItem.ResolvedYouTubeItem == null ||
                categorisedItem.ResolvedYouTubeItem.ShowId != YouTubeIdResolver.Extract(discoveryResult.Urls.YouTube!))
            {
                categorisedItem = categorisedItem with
                {
                    ResolvedYouTubeItem =
                    await youTubeUrlCategoriser.Resolve(null, discoveryResult.Urls.YouTube!, indexingContext)
                };
            }
        }

        var submitResult = await ProcessCategorisedItem(categorisedItem, submitOptions);

        DiscoverySubmitResultState state;
        if (submitResult is {PodcastResult: SubmitResultState.Created, EpisodeResult: SubmitResultState.Created})
        {
            state = DiscoverySubmitResultState.CreatedPodcastAndEpisode;
        }
        else if (submitResult is {PodcastResult: SubmitResultState.Enriched, EpisodeResult: SubmitResultState.Created})
        {
            state = DiscoverySubmitResultState.CreatedEpisode;
        }
        else if (submitResult is {PodcastResult: SubmitResultState.Enriched, EpisodeResult: SubmitResultState.Enriched})
        {
            state = DiscoverySubmitResultState.EnrichedPodcastAndEpisode;
        }
        else
        {
            throw new ArgumentException(
                $"Unknown state: podcast-result: '{submitResult.PodcastResult.ToString()}', episode-result '{submitResult.EpisodeResult.ToString()}'.");
        }

        return new DiscoverySubmitResult(state, submitResult.EpisodeId);
    }

    private async Task<SubmitResult> ProcessCategorisedItem(
        CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
        SubmitResult submitResult;
        if (categorisedItem.MatchingPodcast != null)
        {
            submitResult = await AddEpisodeToExistingPodcast(categorisedItem);

            if (submitOptions.PersistToDatabase)
            {
                await podcastRepository.Save(categorisedItem.MatchingPodcast);
            }
            else
            {
                logger.LogWarning("Bypassing persisting podcast.");
            }
        }
        else
        {
            var (newPodcast, newEpisode) = await CreatePodcastWithEpisode(categorisedItem);
            submitResult = new SubmitResult(SubmitResultState.Created, SubmitResultState.Created, newEpisode.Id);
            if (submitOptions.PersistToDatabase)
            {
                await podcastRepository.Save(newPodcast);
            }
            else
            {
                logger.LogWarning("Bypassing persisting new-podcast.");
            }
        }

        return submitResult;
    }

    private async Task<SubmitResult> AddEpisodeToExistingPodcast(
        CategorisedItem categorisedItem)
    {
        SubmitResultState episodeResult;
        var matchingEpisodes = categorisedItem.MatchingEpisode != null
            ? [categorisedItem.MatchingEpisode]
            : categorisedItem.MatchingPodcast!.Episodes.Where(episode =>
                IsMatchingEpisode(episode, categorisedItem)).ToArray();

        Episode? matchingEpisode;
        if (matchingEpisodes!.Count() > 1)
        {
            var title = categorisedItem.ResolvedAppleItem?.EpisodeTitle ??
                        categorisedItem.ResolvedSpotifyItem?.EpisodeTitle ??
                        categorisedItem.ResolvedYouTubeItem?.EpisodeTitle;
            matchingEpisode = FuzzyMatcher.Match(title!, matchingEpisodes, x => x.Title);
        }
        else
        {
            matchingEpisode = matchingEpisodes.SingleOrDefault();
        }

        logger.LogInformation(
            $"Modifying podcast with name '{categorisedItem.MatchingPodcast!.Name}' and id '{categorisedItem.MatchingPodcast.Id}'.");

        var podcastResult = ApplyResolvedPodcastServiceProperties(
            categorisedItem.MatchingPodcast,
            categorisedItem, matchingEpisode);

        Guid episodeId;
        if (matchingEpisode == null)
        {
            episodeResult = SubmitResultState.Created;
            var episode = CreateEpisode(categorisedItem);
            await subjectEnricher.EnrichSubjects(
                episode,
                new SubjectEnrichmentOptions(
                    categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                    categorisedItem.MatchingPodcast.IgnoredSubjects,
                    categorisedItem.MatchingPodcast.DefaultSubject));
            categorisedItem.MatchingPodcast.Episodes.Add(episode);
            categorisedItem.MatchingPodcast.Episodes =
                categorisedItem.MatchingPodcast.Episodes.OrderByDescending(x => x.Release).ToList();
            episodeId = episode.Id;
        }
        else
        {
            episodeResult = SubmitResultState.Enriched;
            episodeId = matchingEpisode.Id;
        }

        return new SubmitResult(episodeResult, podcastResult, episodeId);
    }

    private async Task<(Podcast, Episode)> CreatePodcastWithEpisode(CategorisedItem categorisedItem)
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
            newPodcast.YouTubePublicationOffset = DefaultMatchingPodcastYouTubePublishingDelay.Ticks;
        }

        var episode = CreateEpisode(categorisedItem);
        await subjectEnricher.EnrichSubjects(episode);
        newPodcast.Episodes.Add(episode);
        logger.LogInformation($"Created podcast with name '{showName}' with id '{newPodcast.Id}'.");

        return (newPodcast, episode);
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
                throw new ArgumentOutOfRangeException(nameof(categorisedItem.Authority));
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

        logger.LogInformation(
            $"Created episode with spotify-id '{categorisedItem.ResolvedSpotifyItem?.EpisodeId}', apple-id '{categorisedItem.ResolvedAppleItem?.EpisodeId}', youtube-id '{categorisedItem.ResolvedYouTubeItem?.EpisodeId}' and episode-id '{newEpisode.Id}'.");
        return newEpisode;
    }

    private SubmitResultState ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        CategorisedItem categorisedItem,
        Episode? matchingEpisode)
    {
        var result = SubmitResultState.None;
        if (matchingEpisode != null)
        {
            logger.LogInformation(
                $"Applying to episode with title '{matchingEpisode.Title}' and id '{matchingEpisode.Id}'.");
        }

        if (categorisedItem.ResolvedAppleItem != null)
        {
            if (!matchingPodcast.AppleId.HasValue)
            {
                matchingPodcast.AppleId = categorisedItem.ResolvedAppleItem.ShowId;
                result = SubmitResultState.Enriched;
                logger.LogInformation(
                    $"Enriched podcast '{matchingPodcast.Id}' with apple details with apple-id {categorisedItem.ResolvedAppleItem.ShowId}.");
            }

            if (matchingEpisode != null)
            {
                if (!matchingEpisode.AppleId.HasValue ||
                    matchingEpisode.AppleId != categorisedItem.ResolvedAppleItem.EpisodeId)
                {
                    matchingEpisode.AppleId = categorisedItem.ResolvedAppleItem.EpisodeId;
                    logger.LogInformation(
                        $"Enriched episode '{matchingEpisode.Id}' with apple details with apple-id {categorisedItem.ResolvedAppleItem.EpisodeId}.");
                }

                if (matchingEpisode.Urls.Apple == null ||
                    matchingEpisode.Urls.Apple != categorisedItem.ResolvedAppleItem.Url)
                {
                    matchingEpisode.Urls.Apple = categorisedItem.ResolvedAppleItem.Url;
                    logger.LogInformation(
                        $"Enriched episode '{matchingEpisode.Id}' with apple details with apple-url {categorisedItem.ResolvedAppleItem.Url}.");
                }

                if (matchingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedAppleItem.Release.TimeOfDay != TimeSpan.Zero)
                {
                    matchingEpisode.Release = categorisedItem.ResolvedAppleItem.Release;
                }

                if (matchingEpisode.Description.EndsWith("...") &&
                    categorisedItem.ResolvedAppleItem.EpisodeDescription.Length > matchingEpisode.Description.Length)
                {
                    matchingEpisode.Description = categorisedItem.ResolvedAppleItem.EpisodeDescription.Trim();
                }
            }
        }

        if (categorisedItem.ResolvedSpotifyItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.SpotifyId))
            {
                matchingPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem.ShowId;
                result = SubmitResultState.Enriched;
                logger.LogInformation(
                    $"Enriched podcast '{matchingPodcast.Id}' with spotify details with spotify-id {categorisedItem.ResolvedSpotifyItem.ShowId}.");
            }

            if (matchingEpisode != null)
            {
                if (string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId) ||
                    matchingEpisode.SpotifyId != categorisedItem.ResolvedSpotifyItem.EpisodeId)
                {
                    matchingEpisode.SpotifyId = categorisedItem.ResolvedSpotifyItem.EpisodeId;
                    logger.LogInformation(
                        $"Enriched episode '{matchingEpisode.Id}' with spotify details with spotify-id {categorisedItem.ResolvedSpotifyItem.EpisodeId}.");
                }

                if (matchingEpisode.Urls.Spotify == null ||
                    matchingEpisode.Urls.Spotify != categorisedItem.ResolvedSpotifyItem.Url)
                {
                    matchingEpisode.Urls.Spotify = categorisedItem.ResolvedSpotifyItem.Url;
                    logger.LogInformation(
                        $"Enriched episode '{matchingEpisode.Id}' with spotify details with spotify-url {categorisedItem.ResolvedSpotifyItem.Url}.");
                }

                if (matchingEpisode.Description.EndsWith("...") &&
                    categorisedItem.ResolvedSpotifyItem.EpisodeDescription.Length > matchingEpisode.Description.Length)
                {
                    matchingEpisode.Description = categorisedItem.ResolvedSpotifyItem.EpisodeDescription.Trim();
                }
            }
        }

        if (categorisedItem.ResolvedYouTubeItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.YouTubeChannelId))
            {
                matchingPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem.ShowId;
                matchingPodcast.YouTubePublicationOffset = DefaultMatchingPodcastYouTubePublishingDelay.Ticks;
                result = SubmitResultState.Enriched;
                logger.LogInformation(
                    $"Enriched podcast '{matchingPodcast.Id}' with youtube details with youtube-id {categorisedItem.ResolvedYouTubeItem.ShowId}.");
            }

            if (matchingEpisode != null)
            {
                if (string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId) ||
                    matchingEpisode.YouTubeId != categorisedItem.ResolvedYouTubeItem.EpisodeId)
                {
                    matchingEpisode.YouTubeId = categorisedItem.ResolvedYouTubeItem.EpisodeId;
                    logger.LogInformation(
                        $"Enriched episode '{matchingEpisode.Id}' with youtube details with youtube-id {categorisedItem.ResolvedYouTubeItem.EpisodeId}.");
                }

                if (matchingEpisode.Urls.YouTube == null ||
                    matchingEpisode.Urls.YouTube != categorisedItem.ResolvedYouTubeItem.Url)
                {
                    matchingEpisode.Urls.YouTube = categorisedItem.ResolvedYouTubeItem.Url;
                    logger.LogInformation(
                        $"Enriched episode '{matchingEpisode.Id}' with youtube details with youtube-url {categorisedItem.ResolvedYouTubeItem.Url}.");
                }

                if (matchingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedYouTubeItem.Release.TimeOfDay != TimeSpan.Zero)
                {
                    matchingEpisode.Release = categorisedItem.ResolvedYouTubeItem.Release;
                }

                if (matchingEpisode.Description.Trim().EndsWith("...") &&
                    categorisedItem.ResolvedYouTubeItem.EpisodeDescription.Length > matchingEpisode.Description.Length)
                {
                    matchingEpisode.Description = categorisedItem.ResolvedYouTubeItem.EpisodeDescription.Trim();
                }
            }
        }

        return result;
    }

    private bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem)
    {
        var spotifyResolved = (categorisedItem.ResolvedSpotifyItem != null &&
                               !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                               episode.SpotifyId != categorisedItem.ResolvedSpotifyItem.EpisodeId) ||
                              categorisedItem.ResolvedSpotifyItem == null;
        var appleResolved = (categorisedItem.ResolvedAppleItem != null && episode.AppleId != null &&
                             episode.AppleId != categorisedItem.ResolvedAppleItem.EpisodeId) ||
                            categorisedItem.ResolvedAppleItem == null;
        var youTubeResolved = (categorisedItem.ResolvedYouTubeItem != null &&
                               !string.IsNullOrWhiteSpace(episode.YouTubeId) &&
                               episode.YouTubeId != categorisedItem.ResolvedYouTubeItem.EpisodeId) ||
                              categorisedItem.ResolvedYouTubeItem == null;
        var alreadyCategorised =
            spotifyResolved &&
            appleResolved &&
            youTubeResolved;
        if (alreadyCategorised)
        {
            return false;
        }

        var matchingSpotify = categorisedItem.ResolvedSpotifyItem != null &&
                              !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                              episode.SpotifyId == categorisedItem.ResolvedSpotifyItem.EpisodeId;
        var matchingApple = categorisedItem.ResolvedAppleItem != null && episode.AppleId != null &&
                            episode.AppleId == categorisedItem.ResolvedAppleItem.EpisodeId;
        var matchingYouTube = categorisedItem.ResolvedYouTubeItem != null &&
                              !string.IsNullOrWhiteSpace(episode.YouTubeId) &&
                              episode.YouTubeId == categorisedItem.ResolvedYouTubeItem.EpisodeId;
        var hasMatchingUrl =
            matchingSpotify ||
            matchingApple ||
            matchingYouTube;
        if (hasMatchingUrl)
        {
            return true;
        }

        var episodeTitle = WebUtility.HtmlDecode(episode.Title.Trim());
        string resolvedTitle;
        if (categorisedItem is {Authority: Service.Apple, ResolvedAppleItem: not null})
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedAppleItem.EpisodeTitle.Trim());
        }
        else if (categorisedItem is {Authority: Service.Spotify, ResolvedSpotifyItem: not null})
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedSpotifyItem.EpisodeTitle.Trim());
        }
        else if (categorisedItem is {Authority: Service.YouTube, ResolvedYouTubeItem: not null})
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedYouTubeItem.EpisodeTitle.Trim());
        }
        else
        {
            return false;
        }

        if (resolvedTitle == episodeTitle ||
            resolvedTitle.Contains(episodeTitle) ||
            episodeTitle.Contains(resolvedTitle))
        {
            return true;
        }

        if (FuzzyMatcher.IsMatch(resolvedTitle, episodeTitle, e => e, MinFuzzyTitleMatch))
        {
            return true;
        }

        return false;
    }
}