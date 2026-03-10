using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;
using Episode= RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.UrlSubmission.Factories;

/// <summary>
/// V2 implementation that creates podcasts and episodes using detached IEpisodeRepository.
/// </summary>
public class PodcastAndEpisodeFactoryV2(
    IEpisodeFactory episodeFactory,
    IPodcastFactory podcastFactory,
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ISubjectEnricher subjectEnricher,
    ILogger<PodcastAndEpisodeFactoryV2> logger
) : IPodcastAndEpisodeFactoryV2
{
    public async Task<CreatePodcastWithEpisodeResponseV2> CreatePodcastWithEpisode(
        CategorisedItem categorisedItem)
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
            case Service.Other:
                showName = categorisedItem.ResolvedNonPodcastServiceItem!.Title!;
                publisher = categorisedItem.ResolvedNonPodcastServiceItem!.Publisher!;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Create legacy podcast for now (until factory supports V2)
        var newPodcast = await podcastFactory.Create(showName);
        newPodcast.Publisher = publisher;
        newPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem?.ShowId ?? string.Empty;
        newPodcast.AppleId = categorisedItem.ResolvedAppleItem?.ShowId;
        newPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem?.ShowId ?? string.Empty;
        newPodcast.YouTubePlaylistId = categorisedItem.ResolvedYouTubeItem?.PlaylistId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(newPodcast.YouTubeChannelId))
        {
            newPodcast.YouTubePublicationOffset = Constants.DefaultMatchingPodcastYouTubePublishingDelay.Ticks;
        }

        // Create legacy episode
        var episode = episodeFactory.CreateEpisode(categorisedItem);
        var subjectsResult = await subjectEnricher.EnrichSubjects(episode);

        logger.LogInformation("Created podcast with name '{ShowName}' with id '{NewPodcastId}'.", showName, newPodcast.Id);

        // Convert to V2 models
        var v2Podcast = new Podcast
        {
            Id = newPodcast.Id,
            Name = newPodcast.Name,
            Language = newPodcast.Language,
            Removed = newPodcast.Removed,
            Publisher = newPodcast.Publisher,
            Bundles = newPodcast.Bundles,
            IndexAllEpisodes = newPodcast.IndexAllEpisodes,
            IgnoreAllEpisodes = newPodcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = newPodcast.BypassShortEpisodeChecking,
            MinimumDuration = newPodcast.MinimumDuration,
            ReleaseAuthority = newPodcast.ReleaseAuthority,
            PrimaryPostService = newPodcast.PrimaryPostService,
            SpotifyId = newPodcast.SpotifyId,
            SpotifyMarket = newPodcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = newPodcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = newPodcast.AppleId,
            YouTubeChannelId = newPodcast.YouTubeChannelId,
            YouTubePlaylistId = newPodcast.YouTubePlaylistId,
            YouTubePublicationOffset = newPodcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = newPodcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = newPodcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = newPodcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = newPodcast.TwitterHandle,
            BlueskyHandle = newPodcast.BlueskyHandle,
            HashTag = newPodcast.HashTag,
            EnrichmentHashTags = newPodcast.EnrichmentHashTags,
            TitleRegex = newPodcast.TitleRegex,
            DescriptionRegex = newPodcast.DescriptionRegex,
            EpisodeMatchRegex = newPodcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = newPodcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = newPodcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = newPodcast.IgnoredSubjects,
            DefaultSubject = newPodcast.DefaultSubject,
            SearchTerms = newPodcast.SearchTerms,
            KnownTerms = newPodcast.KnownTerms,
            FileKey = newPodcast.FileKey,
            Timestamp = newPodcast.Timestamp
        };

        var v2Episode = new Episode
        {
            Id = episode.Id,
            PodcastId = newPodcast.Id,
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
            PodcastName = newPodcast.Name,
            PodcastSearchTerms = newPodcast.SearchTerms,
            Language = episode.Language ?? newPodcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = newPodcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };

        // Save to V2 repositories
        await podcastRepository.Save(v2Podcast);
        await episodeRepository.Save(v2Episode);

        var submitEpisodeDetails = new SubmitEpisodeDetails(
            episode.Urls.Spotify != null,
            episode.Urls.Apple != null,
            episode.Urls.YouTube != null,
            subjectsResult.Additions,
            episode.Urls.BBC != null,
            episode.Urls.InternetArchive != null);

        return new CreatePodcastWithEpisodeResponseV2(v2Podcast, v2Episode, submitEpisodeDetails);
    }
}
