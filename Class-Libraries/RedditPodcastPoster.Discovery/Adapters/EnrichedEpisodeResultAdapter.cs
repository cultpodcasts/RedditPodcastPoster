using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.Subjects.Matching;

namespace RedditPodcastPoster.Discovery.Adapters;

public class EnrichedEpisodeResultAdapter(
    ISubjectMatcher subjectMatcher,
    IEpisodeGuestEnricher guestEnricher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EnrichedEpisodeResultAdapter> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IEnrichedEpisodeResultAdapter
{
    public async Task<DiscoveryResult> ToDiscoveryResult(EnrichedEpisodeResult episode)
    {
        var discoveryResult = new DiscoveryResult
        {
            State = DiscoveryResultState.Unprocessed
        };

        var matchEpisode = new Episode
        {
            Title = episode.EpisodeResult.EpisodeName,
            Description = episode.EpisodeResult.Description
        };

        var subjects = await subjectMatcher.MatchSubjects(matchEpisode);

        discoveryResult.Urls.Apple = episode.EpisodeResult.Urls.Apple;
        discoveryResult.Urls.Spotify = episode.EpisodeResult.Urls.Spotify;
        discoveryResult.Urls.YouTube = episode.EpisodeResult.Urls.YouTube;
        discoveryResult.Sources = episode.EpisodeResult.DiscoverServices;
        discoveryResult.EnrichedTimeFromApple = episode.EpisodeResult.EnrichedTimeFromApple;
        discoveryResult.EnrichedUrlFromSpotify = episode.EpisodeResult.EnrichedUrlFromSpotify;
        discoveryResult.EpisodeName = episode.EpisodeResult.EpisodeName;
        discoveryResult.ShowName = episode.EpisodeResult.ShowName;

        var description = episode.EpisodeResult.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            discoveryResult.Description = description;
        }

        var showDescription = episode.EpisodeResult.ShowDescription;
        if (!string.IsNullOrWhiteSpace(showDescription))
        {
            discoveryResult.ShowDescription = showDescription;
        }

        discoveryResult.Released = episode.EpisodeResult.Released;
        if (episode.EpisodeResult.Length.HasValue)
        {
            discoveryResult.Length = episode.EpisodeResult.Length;
        }

        discoveryResult.Subjects = subjects.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches))
            .Select(x => x.Subject.Name);

        if (episode.EpisodeResult.ViewCount.HasValue || episode.EpisodeResult.MemberCount.HasValue)
        {
            discoveryResult.YouTubeViews = episode.EpisodeResult.ViewCount;
            discoveryResult.YouTubeChannelMembers = episode.EpisodeResult.MemberCount;
        }

        discoveryResult.ContainsSyntheticMedia = episode.EpisodeResult.ContainsSyntheticMedia;
        discoveryResult.ImageUrl = episode.EpisodeResult.ImageUrl;

        await guestEnricher.EnrichGuests(matchEpisode);
        discoveryResult.Guests = matchEpisode.Guests ?? [];

        discoveryResult.MatchingPodcastIds = episode.PodcastResults.Select(x => x.PodcastId).ToArray();
        return discoveryResult;
    }
}
