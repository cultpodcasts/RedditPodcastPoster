using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Factories;

/// <summary>
/// Single factory for catalogue <see cref="EpisodeCandidate"/> → persisted <see cref="Episode"/>.
/// Provider boundaries adapt API types → candidate, then create episodes here — not via scattered <c>Episode.From*</c>.
/// </summary>
public interface IEpisodeFromCandidateFactory
{
    Episode Create(EpisodeCandidate candidate, bool explicitContent);
}
