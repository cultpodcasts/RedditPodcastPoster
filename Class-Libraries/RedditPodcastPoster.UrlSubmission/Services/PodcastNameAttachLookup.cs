using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.UrlSubmission.Services;

public static class PodcastNameAttachLookup
{
    public static async Task<IReadOnlyList<Podcast>> FindByName(
        IPodcastRepository podcastRepository,
        string podcastName,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<Podcast>();
        await foreach (var candidate in podcastRepository
                           .GetAllBy(x => x.Name == podcastName)
                           .WithCancellation(cancellationToken))
        {
            matches.Add(candidate);
        }

        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(podcastName))
        {
            var lowerName = podcastName.ToLower();
            await foreach (var candidate in podcastRepository
                               .GetAllBy(x => x.Name.ToLower() == lowerName)
                               .WithCancellation(cancellationToken))
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }
}
