using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace RedditPodcastPoster.Discovery;

public class EpisodeResultAdapter(
    ISubjectMatcher subjectMatcher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EpisodeResultAdapter> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IEpisodeResultAdapter
{
    public async Task<DiscoveryResult> ToDiscoveryResult(EpisodeResult episode)
    {
        var discoveryResult = new DiscoveryResult();

        var subjects = await subjectMatcher.MatchSubjects(new Episode
            {Title = episode.EpisodeName, Description = episode.Description});

        var description = episode.Description;
        discoveryResult.Urls.Apple = episode.Urls.Apple;
        discoveryResult.Urls.Spotify = episode.Urls.Spotify;
        discoveryResult.Urls.YouTube = episode.Urls.YouTube;

        discoveryResult.Source = episode.DiscoverService
            .ConvertEnumByName<PodcastServices.Abstractions.DiscoverService, DiscoverService>();
        discoveryResult.EnrichmentService = episode.EnrichedFrom
            .ConvertEnumByName<PodcastServices.Abstractions.EnrichmentService, EnrichmentService>();

        discoveryResult.EpisodeName = episode.EpisodeName;

        discoveryResult.ShowName = episode.ShowName;

        if (!string.IsNullOrWhiteSpace(description))
        {
            discoveryResult.Description = description;
        }

        discoveryResult.Released = episode.Released;
        if (episode.Length != null)
        {
            discoveryResult.Length = episode.Length;
        }

        discoveryResult.Subjects = subjects.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches))
            .Select(x => x.Subject.Name);

        if (episode.ViewCount.HasValue || episode.MemberCount.HasValue)
        {
            discoveryResult.YouTubeViews = episode.ViewCount;
            discoveryResult.YouTubeChannelMembers = episode.MemberCount;
        }

        discoveryResult.ImageUrl = episode.ImageUrl;

        return discoveryResult;
    }
}