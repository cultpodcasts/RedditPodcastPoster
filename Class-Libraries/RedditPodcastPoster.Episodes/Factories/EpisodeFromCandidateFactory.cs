using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Factories;

public sealed class EpisodeFromCandidateFactory : IEpisodeFromCandidateFactory
{
    public Episode Create(EpisodeCandidate candidate, bool explicitContent)
    {
        var episode = new Episode
        {
            Title = candidate.Title,
            Description = candidate.Description,
            Length = candidate.Duration,
            Explicit = explicitContent,
            Release = candidate.Release.Value
        };

        if (candidate.SourceLink is not { } link)
        {
            return episode;
        }

        switch (link.Service)
        {
            case Service.Spotify:
                EpisodeServicePresence.SetSpotifyIdentity(episode, link.Id);
                EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, link.Url, link.Image);
                break;
            case Service.Apple:
                if (link.Id != null && long.TryParse(link.Id, out var appleId))
                {
                    EpisodeServicePresence.SetAppleIdentity(episode, appleId);
                }

                EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, link.Url, link.Image);
                break;
            case Service.YouTube:
                EpisodeServicePresence.SetYouTubeIdentity(episode, link.Id);
                EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, link.Url, link.Image);
                break;
        }

        return episode;
    }
}
