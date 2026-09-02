using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Factories;

public class PodcastAndEpisodeFactory(
    IEpisodeFactory episodeFactory,
    IPodcastFactory podcastFactory,
    ISubjectEnricher subjectEnricher,
    IEpisodeGuestEnricher guestEnricher,
    ILogger<PodcastAndEpisodeFactory> logger
) : IPodcastAndEpisodeFactory
{
    public async Task<CreatePodcastWithEpisodeResponse> CreatePodcastWithEpisode(
        CategorisedItem categorisedItem,
        string? podcastName = null)
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
                showName = ResolveNonPodcastShowName(categorisedItem.ResolvedNonPodcastServiceItem!);
                publisher = categorisedItem.ResolvedNonPodcastServiceItem!.Publisher ?? string.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!string.IsNullOrWhiteSpace(podcastName))
        {
            showName = podcastName.Trim();
        }

        var newPodcast = await podcastFactory.Create(showName);
        newPodcast.Publisher = publisher;
        newPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem?.ShowId ?? string.Empty;
        newPodcast.AppleId = categorisedItem.ResolvedAppleItem?.ShowId;
        newPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem?.ShowId ?? string.Empty;
        YouTubePlaylistIdChange.Apply(
            newPodcast,
            categorisedItem.ResolvedYouTubeItem?.PlaylistId ?? string.Empty,
            logger);

        if (!string.IsNullOrWhiteSpace(newPodcast.YouTubeChannelId))
        {
            newPodcast.YouTubePublicationOffset = Constants.DefaultMatchingPodcastYouTubePublishingDelay.Ticks;
        }

        var episode = episodeFactory.CreateEpisode(categorisedItem);
        var subjectsResult = await subjectEnricher.EnrichSubjects(episode);
        var guestsResult = await guestEnricher.EnrichGuests(episode);
        logger.LogInformation("Created podcast with name '{ShowName}' with id '{NewPodcastId}'.", showName, newPodcast.Id);

        var submitEpisodeDetails = SubmitEpisodeDetails.FromEpisode(
            episode,
            subjectsResult.Additions,
            guestsResult.Additions,
            guestsResult.SkippedLowConfidence);
        episode.SetPodcastProperties(newPodcast, inheritLanguageIfUnset: true);
        return new CreatePodcastWithEpisodeResponse(newPodcast, episode, submitEpisodeDetails);
    }

    private static string ResolveNonPodcastShowName(
        ResolvedNonPodcastServiceItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.ShowName))
        {
            return item.ShowName.Trim();
        }

        if (item.NonPodcastService == NonPodcastService.Vimeo &&
            !string.IsNullOrWhiteSpace(item.Publisher))
        {
            return item.Publisher.Trim();
        }

        return item.Title ?? string.Empty;
    }
}