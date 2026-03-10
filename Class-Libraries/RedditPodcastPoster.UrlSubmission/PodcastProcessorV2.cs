using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 implementation that adds episodes to existing podcasts using detached IEpisodeRepository.
/// </summary>
public class PodcastProcessorV2(
    IEpisodeHelper episodeHelper,
    IEpisodeEnricher episodeEnricher,
    IEpisodeFactory episodeFactory,
    ISubjectEnricher subjectEnricher,
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastProcessorV2> logger) : IPodcastProcessorV2
{
    public async Task<SubmitResult> AddEpisodeToExistingPodcast(
        CategorisedItem categorisedItem)
    {
        if (categorisedItem.MatchingPodcast == null)
        {
            throw new ArgumentException("CategorisedItem must have a MatchingPodcast", nameof(categorisedItem));
        }

        // Load existing episodes from detached repository
        var existingEpisodes = await episodeRepository.GetByPodcastId(categorisedItem.MatchingPodcast.Id).ToListAsync();
        var legacyEpisodes = existingEpisodes.Select(ToLegacyEpisode).ToList();

        var matchingEpisodes = categorisedItem.MatchingEpisode != null
            ? [categorisedItem.MatchingEpisode]
            : legacyEpisodes.Where(episode =>
                episodeHelper.IsMatchingEpisode(episode, categorisedItem)).ToArray();

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
            "Modifying podcast with name '{matchingPodcastName}' and id '{matchingPodcastId}'.",
            categorisedItem.MatchingPodcast!.Name, categorisedItem.MatchingPodcast.Id);

        var (podcastResult, appliedEpisodeResult, submitEpisodeDetails) =
            episodeEnricher.ApplyResolvedPodcastServiceProperties(
                categorisedItem.MatchingPodcast,
                categorisedItem,
                matchingEpisode);

        Guid episodeId;
        SubmitResultState episodeResult;
        if (matchingEpisode == null)
        {
            // Create new episode
            episodeResult = SubmitResultState.Created;
            var episode = episodeFactory.CreateEpisode(categorisedItem);
            var subjectsResult = await subjectEnricher.EnrichSubjects(
                episode,
                new SubjectEnrichmentOptions(
                    categorisedItem.MatchingPodcast.IgnoredAssociatedSubjects,
                    categorisedItem.MatchingPodcast.IgnoredSubjects,
                    categorisedItem.MatchingPodcast.DefaultSubject,
                    categorisedItem.MatchingPodcast.DescriptionRegex));

            episodeId = episode.Id;

            // Convert to V2 and save to detached repository
            var v2Episode = new V2Episode
            {
                Id = episode.Id,
                PodcastId = categorisedItem.MatchingPodcast.Id,
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
                Subjects = episode.Subjects ?? [],
                SearchTerms = episode.SearchTerms,
                PodcastName = categorisedItem.MatchingPodcast.Name,
                PodcastSearchTerms = categorisedItem.MatchingPodcast.SearchTerms,
                Language = episode.Language ?? categorisedItem.MatchingPodcast.Language,
                PodcastMetadataVersion = null,
                PodcastRemoved = categorisedItem.MatchingPodcast.Removed,
                Images = episode.Images,
                TwitterHandles = episode.TwitterHandles,
                BlueskyHandles = episode.BlueskyHandles
            };

            await episodeRepository.Save(v2Episode);

            submitEpisodeDetails = new SubmitEpisodeDetails(
                episode.Urls.Spotify != null,
                episode.Urls.Apple != null,
                episode.Urls.YouTube != null,
                subjectsResult.Additions,
                episode.Urls.BBC != null,
                episode.Urls.InternetArchive != null);
        }
        else
        {
            episodeResult = appliedEpisodeResult;

            // If episode was updated, save changes to detached repository
            if (appliedEpisodeResult == SubmitResultState.Enriched)
            {
                var v2Episode = existingEpisodes.First(e => e.Id == matchingEpisode.Id);
                
                // Update V2 episode with changes from matching episode
                v2Episode.SpotifyId = matchingEpisode.SpotifyId;
                v2Episode.AppleId = matchingEpisode.AppleId;
                v2Episode.YouTubeId = matchingEpisode.YouTubeId;
                v2Episode.Urls = matchingEpisode.Urls;
                
                await episodeRepository.Save(v2Episode);
            }
        }

        // Save podcast metadata if updated
        if (podcastResult == SubmitResultState.Enriched)
        {
            var v2Podcast = ToV2Podcast(categorisedItem.MatchingPodcast);
            await podcastRepository.Save(v2Podcast);
        }

        return new SubmitResult(episodeResult, podcastResult, submitEpisodeDetails, matchingEpisode);
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

    private static V2Podcast ToV2Podcast(Podcast legacyPodcast)
    {
        return new V2Podcast
        {
            Id = legacyPodcast.Id,
            Name = legacyPodcast.Name,
            Language = legacyPodcast.Language,
            Removed = legacyPodcast.Removed,
            Publisher = legacyPodcast.Publisher,
            Bundles = legacyPodcast.Bundles,
            IndexAllEpisodes = legacyPodcast.IndexAllEpisodes,
            IgnoreAllEpisodes = legacyPodcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = legacyPodcast.BypassShortEpisodeChecking,
            MinimumDuration = legacyPodcast.MinimumDuration,
            ReleaseAuthority = legacyPodcast.ReleaseAuthority,
            PrimaryPostService = legacyPodcast.PrimaryPostService,
            SpotifyId = legacyPodcast.SpotifyId,
            SpotifyMarket = legacyPodcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = legacyPodcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = legacyPodcast.AppleId,
            YouTubeChannelId = legacyPodcast.YouTubeChannelId,
            YouTubePlaylistId = legacyPodcast.YouTubePlaylistId,
            YouTubePublicationOffset = legacyPodcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = legacyPodcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = legacyPodcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = legacyPodcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = legacyPodcast.TwitterHandle,
            BlueskyHandle = legacyPodcast.BlueskyHandle,
            HashTag = legacyPodcast.HashTag,
            EnrichmentHashTags = legacyPodcast.EnrichmentHashTags,
            TitleRegex = legacyPodcast.TitleRegex,
            DescriptionRegex = legacyPodcast.DescriptionRegex,
            EpisodeMatchRegex = legacyPodcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = legacyPodcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = legacyPodcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = legacyPodcast.IgnoredSubjects,
            DefaultSubject = legacyPodcast.DefaultSubject,
            SearchTerms = legacyPodcast.SearchTerms,
            KnownTerms = legacyPodcast.KnownTerms,
            FileKey = legacyPodcast.FileKey,
            Timestamp = legacyPodcast.Timestamp
        };
    }
}
