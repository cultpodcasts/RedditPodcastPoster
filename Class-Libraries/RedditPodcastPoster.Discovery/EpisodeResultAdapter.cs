using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace RedditPodcastPoster.Discovery;

public class EpisodeResultAdapter(
    ISubjectMatcher subjectMatcher,
    ILogger<EpisodeResultAdapter> logger) : IEpisodeResultAdapter
{
    public async Task<DiscoveryResult> ToDiscoveryResult(EpisodeResult episode)
    {
        var discoveryResult = new DiscoveryResult();

        var subjects = await subjectMatcher.MatchSubjects(new Episode
            {Title = episode.EpisodeName, Description = episode.Description});

        var description = episode.Description;
        if (episode.Url != null)
        {
            discoveryResult.Url = episode.Url;
        }

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
            discoveryResult.Views = episode.ViewCount;
            discoveryResult.MemberCount = episode.MemberCount;
        }

        return discoveryResult;
    }
}