using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// A news organisation's / TV show's stored playables as the "episodes" of its transient podcast, so submit matches a
/// URL against them like a podcast's episodes. Same projection as the #1050 indexer
/// (<c>CatalogueParentsUpdater.ToEpisode</c>, which UrlSubmission cannot reference), plus guests so a known item is
/// not re-enriched on every resubmit.
/// </summary>
public static class CatalogueParentKnownEpisodes
{
    public static Episode ToEpisode(Playable playable, Podcast transient)
    {
        var episode = new Episode
        {
            Id = playable.Id,
            PodcastId = transient.Id,
            PodcastName = transient.Name,
            Title = playable.Title,
            Description = playable.Description,
            Length = playable.Length,
            Explicit = playable.Explicit,
            Posted = playable.Posted,
            Tweeted = playable.Tweeted,
            TweetId = playable.TweetId,
            Ignored = playable.Ignored,
            Removed = playable.Removed,
            Language = playable.Language,
            Subjects = [.. playable.Subjects],
            RemovedSubjects = [.. playable.RemovedSubjects],
            SearchTerms = playable.SearchTerms,
            HashTag = playable.HashTag,
            Guests = playable.Guests is null ? null : [.. playable.Guests],
            Services = playable.Services?.ToDictionary(
                pair => pair.Key,
                pair => new ServiceLink { Url = pair.Value.Url, Image = pair.Value.Image, Language = pair.Value.Language })
        };
        episode.SetRelease(playable.Release);
        episode.Ids = IdsFromServices(playable.Services);
        return episode;
    }

    /// <summary>Spotify / Apple / YouTube ids parsed from <c>services</c> URLs (playables store no provider ids).</summary>
    public static EpisodeIds? IdsFromServices(IReadOnlyDictionary<string, ServiceLink>? services)
    {
        if (services is null || services.Count == 0)
        {
            return null;
        }

        var ids = new EpisodeIds();
        if (services.TryGetValue(ServiceKeys.Spotify, out var spotify) && spotify.Url is not null)
        {
            var spotifyId = SpotifyIdResolver.GetEpisodeId(spotify.Url);
            ids.Spotify = string.IsNullOrWhiteSpace(spotifyId) ? null : spotifyId;
        }

        if (services.TryGetValue(ServiceKeys.Apple, out var apple) && apple.Url is not null)
        {
            ids.Apple = AppleIdResolver.GetEpisodeId(apple.Url);
        }

        if (services.TryGetValue(ServiceKeys.YouTube, out var youTube) && youTube.Url is not null)
        {
            ids.YouTube = YouTubeIdResolver.Extract(youTube.Url);
        }

        return ids.IsEmpty ? null : ids;
    }
}
