using System.Text.RegularExpressions;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.EliminationTerms;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace EliminateExistingEpisodes;

public class Processor(
    IPodcastRepositoryV2 repository,
    IEpisodeRepository episodeRepository,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    SearchClient searchClient,
    ILogger<Processor> logger)
{
    public async Task Run(Request request)
    {
        Guid podcastId;
        if (request.PodcastId.HasValue)
        {
            podcastId = request.PodcastId.Value;
        }
        else if (request.PodcastName != null)
        {
            var podcastIds = await repository.GetAllBy(x =>
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

        var podcast = await repository.GetBy(x => x.Id == podcastId);
        if (podcast == null)
        {
            throw new InvalidOperationException($"Podcast with id '{podcastId}' not found.");
        }

        var episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();
        var servicePodcast = CreateServicePodcast(podcast, episodes);

        var eliminationTermsProvider = await eliminationTermsProviderInstance.GetAsync();
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(podcast, episodes, eliminationTerms.Terms);
        logger.LogInformation(filterResult.ToString());
        foreach (var filteredEpisode in filterResult.FilteredEpisodes)
        {
            await DeleteSearchDocument(filteredEpisode.Episode.Id);
        }

        if (!string.IsNullOrWhiteSpace(servicePodcast.EpisodeIncludeTitleRegex))
        {
            var includeEpisodeRegex = new Regex(servicePodcast.EpisodeIncludeTitleRegex, Podcast.EpisodeIncludeTitleFlags);
            foreach (var serviceEpisode in servicePodcast.Episodes.Where(x => !x.Removed))
            {
                if (!includeEpisodeRegex.IsMatch(serviceEpisode.Title))
                {
                    serviceEpisode.Removed = true;
                    logger.LogInformation(
                        "Removing episode '{episodeTitle}' of podcast '{podcastName}' due to mismatch with '{episodeIncludeTitleRegex}'.",
                        serviceEpisode.Title, servicePodcast.Name, servicePodcast.EpisodeIncludeTitleRegex);
                    await DeleteSearchDocument(serviceEpisode.Id);
                }
            }
        }

        var episodesById = episodes.ToDictionary(x => x.Id);
        foreach (var filteredEpisode in filterResult.FilteredEpisodes)
        {
            if (episodesById.TryGetValue(filteredEpisode.Episode.Id, out var detachedEpisode))
            {
                detachedEpisode.Removed = true;
                await episodeRepository.Save(detachedEpisode);
            }
        }

        foreach (var serviceEpisode in servicePodcast.Episodes)
        {
            if (serviceEpisode.Removed &&
                episodesById.TryGetValue(serviceEpisode.Id, out var detachedEpisode) &&
                !detachedEpisode.Removed)
            {
                detachedEpisode.Removed = true;
                await episodeRepository.Save(detachedEpisode);
            }
        }
    }

    private async Task DeleteSearchDocument(Guid episodeId)
    {
        var result = await searchClient.DeleteDocumentsAsync(
            "id",
            [episodeId.ToString()],
            new IndexDocumentsOptions { ThrowOnAnyError = true },
            CancellationToken.None);
        var success = result.Value.Results.First().Succeeded;
        if (!success)
        {
            logger.LogError("Error removing search-item with episode-id '{episodeId}', message: '{mesage}'.",
                episodeId, result.Value.Results.First().ErrorMessage);
        }
    }

    private static Podcast CreateServicePodcast(V2Podcast podcast, IEnumerable<V2Episode> episodes)
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
            Timestamp = podcast.Timestamp,
            Episodes = episodes.Select(ToLegacyEpisode).ToList()
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
            Language = episode.Language,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}