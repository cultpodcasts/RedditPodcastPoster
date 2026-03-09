using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.Text;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace EnrichExistingEpisodesFromPodcastServices;

public class EnrichPodcastEpisodesProcessor(
    IPodcastRepositoryV2 podcastsRepository,
    IEpisodeRepository episodeRepository,
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IAppleEpisodeResolver appleEpisodeResolver,
    IHtmlSanitiser htmlSanitiser,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<EnrichPodcastEpisodesProcessor> logger)
{
    public async Task Run(EnrichPodcastEpisodesRequest request)
    {
        IndexingContext indexingContext;
        List<Guid> updatedEpisodeIds = new();
        if (request.ReleasedSince.HasValue)
        {
            indexingContext = new IndexingContext(DateTimeExtensions.DaysAgo(request.ReleasedSince.Value));
        }
        else
        {
            indexingContext = new IndexingContext();
        }

        indexingContext = indexingContext with
        {
            SkipExpensiveSpotifyQueries = !request.AllowExpensiveQueries,
            SkipExpensiveYouTubeQueries = !request.AllowExpensiveQueries,
            SkipYouTubeUrlResolving = request.SkipYouTubeUrlResolving
        };

        Guid podcastId;
        if (request.PodcastId.HasValue)
        {
            podcastId = request.PodcastId.Value;
        }
        else if (request.PodcastName != null)
        {
            var podcastIds = await podcastsRepository.GetAllBy(x =>
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Id)
                .ToListAsync();
            if (!podcastIds.Any())
            {
                throw new InvalidOperationException($"No podcast matching '{request.PodcastName}' could be found.");
            }

            if (podcastIds.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple podcasts matching '{request.PodcastName}' were found. Ids: {string.Join(", ", podcastIds)}.");
            }

            podcastId = podcastIds.First();
        }
        else
        {
            throw new InvalidOperationException("A podcast-id or podcast-name must be provided.");
        }

        var podcast = await podcastsRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast found with id '{request.PodcastId}'.");
        }

        IAsyncEnumerable<V2Episode> episodesQuery = episodeRepository.GetByPodcastId(podcastId);
        if (request.ReleasedSince.HasValue)
        {
            episodesQuery = episodeRepository.GetAllBy(x => x.PodcastId == podcastId && x.Release >= indexingContext.ReleasedSince);
        }

        var servicePodcast = ToLegacyPodcast(podcast);

        await foreach (var detachedEpisode in episodesQuery)
        {
            var episodeUpdated = false;
            var episode = ToLegacyEpisode(detachedEpisode);
            var criteria = new PodcastServiceSearchCriteria(servicePodcast.Name, string.Empty, servicePodcast.Publisher,
                episode.Title, episode.Description, episode.Release, episode.Length);

            if (!string.IsNullOrWhiteSpace(servicePodcast.YouTubeChannelId) &&
                !string.IsNullOrWhiteSpace(servicePodcast.SpotifyId) &&
                !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                episode.AppleId == null)
            {
                var spotifyEpisode =
                    await spotifyEpisodeResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(servicePodcast, episode),
                        indexingContext);
                if (spotifyEpisode?.FullEpisode != null &&
                    spotifyEpisode.FullEpisode.Name.Trim() != episode.Title.Trim())
                {
                    criteria.SpotifyTitle = spotifyEpisode.FullEpisode.Name.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(servicePodcast.YouTubeChannelId) &&
                servicePodcast.AppleId != null &&
                episode.AppleId != null &&
                string.IsNullOrWhiteSpace(episode.SpotifyId))
            {
                var appleEpisode =
                    await appleEpisodeResolver.FindEpisode(FindAppleEpisodeRequestFactory.Create(servicePodcast, episode),
                        indexingContext);
                if (appleEpisode != null && appleEpisode.Title.Trim() != episode.Title.Trim())
                {
                    criteria.AppleTitle = appleEpisode.Title.Trim();
                }
            }

            if (servicePodcast.AppleId != null && (episode.AppleId == null || episode.Urls.Apple == null))
            {
                var match = await appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Apple ??= match.Url;
                    episode.AppleId ??= match.EpisodeId;
                    var appleImage = match.Image;
                    if (appleImage != null)
                    {
                        episode.Images ??= new EpisodeImages();
                        episode.Images.Apple = appleImage;
                    }

                    logger.LogInformation("Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId,
                        match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((!string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null) &&
                        servicePodcast.ReleaseAuthority == Service.YouTube)
                    {
                        var spotifyEpisode =
                            await spotifyEpisodeResolver.FindEpisode(
                                FindSpotifyEpisodeRequestFactory.Create(servicePodcast, episode), indexingContext);
                        if (spotifyEpisode.FullEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(servicePodcast.Name, string.Empty,
                                servicePodcast.Publisher, spotifyEpisode.FullEpisode.Name,
                                htmlSanitiser.Sanitise(spotifyEpisode.FullEpisode.HtmlDescription),
                                spotifyEpisode.FullEpisode.GetReleaseDate(),
                                spotifyEpisode.FullEpisode.GetDuration());
                            match = await appleUrlCategoriser.Resolve(refinedCriteria, podcast, indexingContext);
                            if (match != null)
                            {
                                episode.Urls.Apple ??= match.Url;
                                episode.AppleId ??= match.EpisodeId;
                                var appleImage = match.Image;
                                if (appleImage != null)
                                {
                                    episode.Images ??= new EpisodeImages();
                                    episode.Images.Apple = appleImage;
                                }

                                logger.LogInformation(
                                    "Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId,
                                    match.Url);
                                episodeUpdated = true;
                            }
                        }
                    }
                }
            }

            if (servicePodcast.YouTubeChannelId != null &&
                (string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube == null))
            {
                if (string.IsNullOrWhiteSpace(episode.YouTubeId) && episode.Urls.YouTube != null)
                {
                    var youTubeId = YouTubeIdResolver.Extract(episode.Urls.YouTube);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        episode.YouTubeId = youTubeId;
                        logger.LogInformation(
                            "Enriched from youtube-url: '{UrlsYouTube}', youtube-id: '{EpisodeYouTubeId}'.",
                            episode.Urls.YouTube, episode.YouTubeId);
                    }
                }
                else if (episode.Urls.YouTube == null && !string.IsNullOrWhiteSpace(episode.YouTubeId))
                {
                    episode.Urls.YouTube = SearchResultExtensions.ToYouTubeUrl(episode.YouTubeId);
                    logger.LogInformation(
                        "Enriched from youtube-id: '{EpisodeYouTubeId}', Url: '{UrlsYouTube}'.", episode.YouTubeId,
                        episode.Urls.YouTube);
                }
                else
                {
                    var match = await youTubeUrlCategoriser.Resolve(criteria, servicePodcast, indexingContext);
                    if (match != null)
                    {
                        episode.Urls.YouTube ??= match.Url;
                        if (string.IsNullOrWhiteSpace(episode.YouTubeId))
                        {
                            episode.YouTubeId = match.EpisodeId;
                        }

                        var youTubeImage = match.Image;
                        if (youTubeImage != null)
                        {
                            episode.Images ??= new EpisodeImages();
                            episode.Images.YouTube = youTubeImage;
                        }

                        logger.LogInformation(
                            "Enriched episode with episode-id '{EpisodeId}' from youtube: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                            episode.Id, match.EpisodeId, match.Url);
                        episodeUpdated = true;
                    }
                }
            }

            if (servicePodcast.SpotifyId != null &&
                (string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify == null))
            {
                var match = await spotifyUrlCategoriser.Resolve(criteria, servicePodcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Spotify ??= match.Url;
                    if (string.IsNullOrWhiteSpace(episode.SpotifyId))
                    {
                        episode.SpotifyId = match.EpisodeId;
                    }

                    var spotifyImage = match.Image;
                    if (spotifyImage != null)
                    {
                        episode.Images ??= new EpisodeImages();
                        episode.Images.Spotify = spotifyImage;
                    }

                    logger.LogInformation("Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId,
                        match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((episode.AppleId != null || episode.Urls.Apple != null) &&
                        servicePodcast.ReleaseAuthority == Service.YouTube)
                    {
                        var appleEpisode =
                            await appleEpisodeResolver.FindEpisode(
                                FindAppleEpisodeRequestFactory.Create(servicePodcast, episode), indexingContext);
                        if (appleEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(servicePodcast.Name, string.Empty,
                                servicePodcast.Publisher, appleEpisode.Title, appleEpisode.Description,
                                appleEpisode.Release,
                                appleEpisode.Duration);
                            match = await spotifyUrlCategoriser.Resolve(refinedCriteria, servicePodcast, indexingContext);
                            if (match != null)
                            {
                                episode.Urls.Spotify ??= match.Url;
                                episode.SpotifyId = match.EpisodeId;
                                var spotifyImage = match.Image;
                                if (spotifyImage != null)
                                {
                                    episode.Images ??= new EpisodeImages();
                                    episode.Images.Spotify = spotifyImage;
                                }

                                logger.LogInformation(
                                    "Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId,
                                    match.Url);
                                episodeUpdated = true;
                            }
                        }
                    }
                }
            }

            if (episodeUpdated)
            {
                ApplyLegacyEpisodeUpdates(detachedEpisode, episode);
                await episodeRepository.Save(detachedEpisode);
                updatedEpisodeIds.Add(episode.Id);
            }
        }

        if (updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
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

    private static Episode ToLegacyEpisode(V2Episode episode)
    {
        return new Episode
        {
            Id = episode.Id,
            PodcastId = episode.PodcastId,
            PodcastName = episode.PodcastName,
            PodcastSearchTerms = episode.PodcastSearchTerms,
            SearchLanguage = episode.SearchLanguage,
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
            Subjects = episode.Subjects,
            SearchTerms = episode.SearchTerms,
            Images = episode.Images,
            Language = episode.SearchLanguage,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }

    private static void ApplyLegacyEpisodeUpdates(V2Episode target, Episode source)
    {
        target.Title = source.Title;
        target.Description = source.Description;
        target.Release = source.Release;
        target.Length = source.Length;
        target.Explicit = source.Explicit;
        target.Posted = source.Posted;
        target.Tweeted = source.Tweeted;
        target.BlueskyPosted = source.BlueskyPosted;
        target.Ignored = source.Ignored;
        target.Removed = source.Removed;
        target.SpotifyId = source.SpotifyId;
        target.AppleId = source.AppleId;
        target.YouTubeId = source.YouTubeId;
        target.Urls = source.Urls;
        target.Subjects = source.Subjects;
        target.SearchTerms = source.SearchTerms;
        target.Images = source.Images;
        target.SearchLanguage = source.Language;
        target.TwitterHandles = source.TwitterHandles;
        target.BlueskyHandles = source.BlueskyHandles;
    }
}