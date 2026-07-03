using RedditPodcastPoster.Episodes.Domain;

namespace RedditPodcastPoster.Episodes.Adapters;

public interface IEpisodeCatalogueAdapter<in TInput>
{
    EpisodeCandidate Adapt(TInput input);
}
