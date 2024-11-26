using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

namespace RedditPodcastPoster.Discovery;

public class EnrichedEpisodeResultAdapter(
    ISubjectMatcher subjectMatcher,
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

        var subjects = await subjectMatcher.MatchSubjects(new Episode
            {Title = episode.EpisodeResult.EpisodeName, Description = episode.EpisodeResult.Description});

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

        discoveryResult.ImageUrl = episode.EpisodeResult.ImageUrl;

        discoveryResult.MatchingPodcastIds = episode.PodcastResults.Select(x => x.PodcastId).ToArray();
        return discoveryResult;
    }
}