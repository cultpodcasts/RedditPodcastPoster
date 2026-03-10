using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;
using SpotifyAPI.Web;
using System.Text.RegularExpressions;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace AddAudioPodcast;

public class AddAudioPodcastProcessor(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ISpotifyClientWrapper spotifyClient,
    IPodcastFactory podcastFactory,
    IApplePodcastEnricher applePodcastEnricher,
    ISpotifyPodcastEnricher spotifyPodcastEnricher,
    IPodcastUpdater podcastUpdater,
    iTunesSearchManager iTunesSearchManager,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ISubjectEnricher subjectEnricher,
    ILogger<AddAudioPodcastProcessor> logger)
{
    private readonly IndexingContext _indexingContext = new(SkipYouTubeUrlResolving: true);

    public async Task Create(AddAudioPodcastRequest request)
    {
        var indexingContext = new IndexingContext { SkipPodcastDiscovery = false };
        V2Podcast? podcast = null;
        if (!request.AppleReleaseAuthority)
        {
            var matchingPodcasts = await podcastRepository
                .GetAllBy(x => x.SpotifyId == request.PodcastId).ToListAsync();
            if (matchingPodcasts.Any())
            {
                throw new InvalidOperationException(
                    $"Found podcasts with spotify-id '{request.PodcastId}'. Podcast-ids: {string.Join(",", matchingPodcasts.Select(x => x.Id))}.");
            }

            try
            {
                podcast = await GetSpotifyPodcast(request, indexingContext);
            }
            catch (APITooManyRequestsException e)
            {
                logger.LogError(e, "{exception}: Retry-after: {retryAfter}.", e.GetType(), e.RetryAfter);
                throw;
            }
        }
        else
        {
            var appleId = long.Parse(request.PodcastId);
            var matchingPodcasts =
                await podcastRepository.GetAllBy(x => x.AppleId == appleId).ToListAsync();
            if (matchingPodcasts.Any())
            {
                throw new InvalidOperationException(
                    $"Found podcasts with apple-id '{request.PodcastId}'. Podcast-ids: {string.Join(",", matchingPodcasts.Select(x => x.Id))}.");
            }

            podcast = await GetApplePodcast(request, indexingContext);
        }

        if (podcast != null)
        {
            var result = await podcastUpdater.Update(podcast, false, _indexingContext);

            var episodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();

            if (!string.IsNullOrWhiteSpace(request.EpisodeTitleRegex))
            {
                var includeEpisodesRegex = new Regex(request.EpisodeTitleRegex,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);

                podcast.IndexAllEpisodes = false;
                podcast.EpisodeIncludeTitleRegex = request.EpisodeTitleRegex;

                var episodesToRemove = new List<V2Episode>();
                foreach (var episode in episodes)
                {
                    var match = includeEpisodesRegex.Match(episode.Title);
                    if (!match.Success)
                    {
                        episodesToRemove.Add(episode);
                    }
                }

                foreach (var episode in episodesToRemove)
                {
                    await episodeRepository.Delete(episode.PodcastId, episode.Id);
                    logger.LogInformation("Removed episode '{EpisodeTitle}' due to regex.", episode.Title);
                }

                episodes = episodes.Except(episodesToRemove).ToList();
            }

            var episodesToUpdate = new List<V2Episode>();
            foreach (var episode in episodes)
            {
                await subjectEnricher.EnrichSubjects(episode,
                    new SubjectEnrichmentOptions(
                        podcast.IgnoredAssociatedSubjects,
                        podcast.IgnoredSubjects,
                        podcast.DefaultSubject,
                        podcast.DescriptionRegex));

                var v2Episode = episodes.FirstOrDefault(e => e.Id == episode.Id);
                if (v2Episode != null)
                {
                    v2Episode.Subjects = episode.Subjects ?? [];
                    episodesToUpdate.Add(v2Episode);
                }
            }

            if (episodesToUpdate.Any())
            {
                await episodeRepository.Save(episodesToUpdate);
            }

            await podcastRepository.Save(podcast);

            if (result.Success)
            {
                logger.LogInformation(result.ToString());
            }
            else
            {
                logger.LogError(result.ToString());
            }

            await episodeSearchIndexerService.IndexEpisodes(episodes.Select(x => x.Id), CancellationToken.None);
        }
        else
        {
            var source = request.AppleReleaseAuthority ? "Apple" : "Spotify";
            logger.LogError("Could not find podcast with id '{RequestPodcastId}' using '{Source}' as source.", request.PodcastId, source);
        }
    }

    private async Task<V2Podcast> GetSpotifyPodcast(AddAudioPodcastRequest request, IndexingContext indexingContext)
    {
        var spotifyPodcast =
            await spotifyClient.GetFullShow(request.PodcastId, new ShowRequest { Market = Market.CountryCode }, new IndexingContext());
        logger.LogInformation("Retrieved spotify podcast");
        var podcast = await podcastRepository.GetBy(x => x.SpotifyId == request.PodcastId);
        if (podcast == null)
        {
            podcast = await podcastFactory.Create(spotifyPodcast.Name);
            podcast.SpotifyId = spotifyPodcast.Id;
            podcast.Bundles = false;
            podcast.Publisher = spotifyPodcast.Publisher.Trim();
            podcast.SpotifyMarket = request.SpotifyMarket;

            await applePodcastEnricher.AddId(podcast);
        }

        return podcast;
    }

    private async Task<V2Podcast?> GetApplePodcast(AddAudioPodcastRequest request, IndexingContext indexingContext)
    {
        var id = long.Parse(request.PodcastId);
        var applePodcast = (await iTunesSearchManager.GetPodcastById(id)).Podcasts.SingleOrDefault();
        logger.LogInformation("Retrieved apple podcast");

        if (applePodcast == null)
        {
            logger.LogError("No apple-podcast found for apple-id '{Id}'.", id);
            return null;
        }

        var podcast = await podcastRepository.GetBy(x => x.AppleId == id);
        if (podcast == null)
        {
            podcast = await podcastFactory.Create(applePodcast.Name);
            podcast.AppleId = applePodcast.Id;
            podcast.Bundles = false;
            podcast.Publisher = applePodcast.ArtistName.Trim();
            podcast.ReleaseAuthority = Service.Apple;

            var legacyForSpotifyEnricher = ToLegacyPodcast(podcast);
            await spotifyPodcastEnricher.AddIdAndUrls(legacyForSpotifyEnricher, indexingContext);
            podcast.SpotifyId = legacyForSpotifyEnricher.SpotifyId;
            podcast.SpotifyEpisodesQueryIsExpensive = legacyForSpotifyEnricher.SpotifyEpisodesQueryIsExpensive;
        }

        return podcast;
    }

    private static V2Podcast ToV2Podcast(Podcast podcast)
    {
        return new V2Podcast
        {
            Id = podcast.Id,
            Name = podcast.Name,
            Language = podcast.Language,
            Removed = podcast.Removed,
            Publisher = podcast.Publisher,
            Bundles = podcast.Bundles,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            MinimumDuration = podcast.MinimumDuration,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            SpotifyId = podcast.SpotifyId,
            SpotifyMarket = podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = podcast.AppleId,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = podcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = podcast.TwitterHandle,
            BlueskyHandle = podcast.BlueskyHandle,
            HashTag = podcast.HashTag,
            EnrichmentHashTags = podcast.EnrichmentHashTags,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = podcast.IgnoredSubjects,
            DefaultSubject = podcast.DefaultSubject,
            SearchTerms = podcast.SearchTerms,
            KnownTerms = podcast.KnownTerms,
            FileKey = podcast.FileKey,
            Timestamp = podcast.Timestamp
        };
    }

    private static Podcast ToLegacyPodcast(V2Podcast podcast)
    {
        return new Podcast(podcast.Id)
        {
            Name = podcast.Name,
            Language = podcast.Language,
            Removed = podcast.Removed,
            Publisher = podcast.Publisher,
            Bundles = podcast.Bundles,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            MinimumDuration = podcast.MinimumDuration,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            SpotifyId = podcast.SpotifyId,
            SpotifyMarket = podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = podcast.AppleId,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = podcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = podcast.TwitterHandle,
            BlueskyHandle = podcast.BlueskyHandle,
            HashTag = podcast.HashTag,
            EnrichmentHashTags = podcast.EnrichmentHashTags,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = podcast.IgnoredSubjects,
            DefaultSubject = podcast.DefaultSubject,
            SearchTerms = podcast.SearchTerms,
            KnownTerms = podcast.KnownTerms,
            FileKey = podcast.FileKey,
            Timestamp = podcast.Timestamp
        };
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
            Language = v2Episode.Language,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }
}