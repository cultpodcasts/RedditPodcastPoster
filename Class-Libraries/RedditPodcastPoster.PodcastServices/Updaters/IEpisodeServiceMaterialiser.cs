using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Turns a podcast episode's nested <c>ids.spotify</c> / <c>ids.apple</c> / <c>ids.youtube</c> into
/// <c>services.{key}.url</c> (and <c>services.{key}.image</c>) before the episode is mapped to a catalogue
/// playable (Film / TV episode / news report). Playables keep Services only — they have no nested ids — so an id
/// without a Services entry would otherwise be lost on Move / kind transfer.
/// </summary>
public interface IEpisodeServiceMaterialiser
{
    /// <summary>
    /// For each platform with an id but no <c>services.{key}.url</c>: look the id up through the existing
    /// podcast-service clients and fill the url and image they return (client image preferred; an existing image is
    /// kept when the client returns none). Anything a client cannot supply falls back to a deterministic url from
    /// the id. Never throws for a client failure; honours cancellation.
    /// </summary>
    Task MaterialiseAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default);

    /// <summary><see cref="MaterialiseAsync(Podcast, Episode, CancellationToken)"/> for every episode, in order.</summary>
    Task MaterialiseAsync(Podcast podcast, IEnumerable<Episode> episodes, CancellationToken cancellationToken = default);
}
