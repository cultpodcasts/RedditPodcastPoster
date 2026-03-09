using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Extensions;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace EnrichPodcastWithImages;

public class Processor(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IImageUpdater imageUpdater,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<Processor> logger)
{
    public async Task Run(Request request)
    {
        List<Guid> podcastIds;
        Func<V2Episode, bool> episodeSelector;
        if (!string.IsNullOrWhiteSpace(request.PodcastPartialMatch))
        {
            var ids = await podcastRepository.GetAllBy(
                    x => x.Name.ToLower().Contains(request.PodcastPartialMatch.ToLower()))
                .ToListAsync();
            podcastIds = ids.Select(x => x.Id).ToList();
            episodeSelector = _ => true;
        }
        else
        {
            var ids = new HashSet<Guid>();
            await foreach (var episode in episodeRepository.GetAllBy(x => x.Subjects.Contains(request.Subject)))
            {
                ids.Add(episode.PodcastId);
            }

            podcastIds = ids.ToList();
            episodeSelector = episode => episode.Subjects.Contains(request.Subject);
        }

        if (!podcastIds.Any())
        {
            logger.LogError("No podcasts found for partial-name '{podcastPartialName}'.",
                request.PodcastPartialMatch);
            return;
        }

        var indexingContext = new IndexingContext();
        var updatedEpisodeIds = new List<Guid>();
        foreach (var podcastId in podcastIds)
        {
            var updatedEpisodes = 0;

            var podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
            if (podcast == null)
            {
                logger.LogError("No podcast with podcast-id '{podcastId}' found.", podcastId);
                continue;
            }

            logger.LogInformation("Enriching podcast '{podcastName}'.", podcast.Name);
            var episodes = await episodeRepository.GetByPodcastId(podcastId)
                .Where(episodeSelector)
                .ToListAsync();

            foreach (var episode in episodes)
            {
                var servicePodcast = CreateServicePodcast(podcast);
                var serviceEpisode = CreateServiceEpisode(episode);
                var imageUpdateRequest = (servicePodcast, serviceEpisode).ToEpisodeImageUpdateRequest();
                var updated = await imageUpdater.UpdateImages(servicePodcast, serviceEpisode, imageUpdateRequest, indexingContext);
                if (updated)
                {
                    ApplyServiceEpisodeUpdates(episode, serviceEpisode);
                    await episodeRepository.Save(episode);
                    updatedEpisodeIds.Add(episode.Id);
                    updatedEpisodes++;
                }
            }

            logger.LogInformation("Updated {updatedEpisodes} episodes.", updatedEpisodes);
        }

        if (updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }

    private static Podcast CreateServicePodcast(V2Podcast podcast)
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

    private static Episode CreateServiceEpisode(V2Episode episode)
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

    private static void ApplyServiceEpisodeUpdates(V2Episode target, Episode source)
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