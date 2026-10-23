namespace RedditPodcastPoster.Subjects.Categorisation;

/// <summary>
/// Gives recent TV episodes and news reports subjects (the parent's default subject and the
/// categoriser). Saves each playable in its own container.
/// Podcast episodes stay on <see cref="IRecentPodcastEpisodeCategoriser"/>, which writes the
/// episode container — this type must not.
/// </summary>
public interface IRecentCataloguePlayableCategoriser
{
    /// <summary>Ids of playables whose subjects were saved.</summary>
    Task<IList<Guid>> Categorise();
}
