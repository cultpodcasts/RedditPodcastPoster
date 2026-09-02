using System.Linq.Expressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public class UrlMembershipLookup(
    IEpisodeRepository episodeRepository,
    IPodcastRepository podcastRepository,
    INonPodcastServiceAdapterResolver nonPodcastServiceAdapterResolver)
    : IUrlMembershipLookup
{
    public async Task<UrlMembershipLookupResult> Lookup(Uri url, CancellationToken cancellationToken)
    {
        var kind = Classify(url, out var storedUrlEquals);
        if (kind == UrlMembershipLookupKinds.Unrecognised || storedUrlEquals is null)
        {
            return new UrlMembershipLookupResult(false, UrlMembershipLookupKinds.Unrecognised);
        }

        var storedMatches = episodeRepository.GetAllBy(storedUrlEquals);
        var matchingPodcastIds = storedMatches is null
            ? []
            : (await storedMatches.Select(x => x.PodcastId).ToListAsync(cancellationToken))
            .Distinct()
            .ToList();

        if (matchingPodcastIds.Count > 1)
        {
            return new UrlMembershipLookupResult(
                false,
                Kind: kind,
                Ambiguous: true,
                PodcastIds: matchingPodcastIds);
        }

        if (matchingPodcastIds.Count == 1)
        {
            var podcastId = matchingPodcastIds[0];
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null)
            {
                return new UrlMembershipLookupResult(
                    true,
                    Kind: kind,
                    PodcastId: podcast.Id,
                    PodcastName: podcast.Name);
            }
        }

        return new UrlMembershipLookupResult(false, kind);
    }

    private string Classify(Uri url, out Expression<Func<Episode, bool>>? storedUrlEquals)
    {
        if (SpotifyPodcastServiceMatcher.IsMatch(url))
        {
            storedUrlEquals = episode =>
                episode.Services != null && episode.Services[ServiceKeys.Spotify].Url == url;
            return UrlMembershipLookupKinds.PodcastService;
        }

        if (ApplePodcastServiceMatcher.IsMatch(url))
        {
            storedUrlEquals = episode =>
                episode.Services != null && episode.Services[ServiceKeys.Apple].Url == url;
            return UrlMembershipLookupKinds.PodcastService;
        }

        if (YouTubePodcastServiceMatcher.IsMatch(url))
        {
            storedUrlEquals = episode =>
                episode.Services != null && episode.Services[ServiceKeys.YouTube].Url == url;
            return UrlMembershipLookupKinds.PodcastService;
        }

        var adapter = nonPodcastServiceAdapterResolver.ForSubmit(url);
        if (adapter != null)
        {
            storedUrlEquals = adapter.StoredUrlEquals(url);
            return UrlMembershipLookupKinds.Streaming;
        }

        storedUrlEquals = null;
        return UrlMembershipLookupKinds.Unrecognised;
    }
}
