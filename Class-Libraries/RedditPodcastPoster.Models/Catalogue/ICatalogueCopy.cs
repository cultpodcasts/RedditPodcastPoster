namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Shared blurb / language / search-term text on both publishers and playables
/// (avoids putting these on <see cref="IPlayable"/> while Film already inherits them from
/// <see cref="Publisher"/>).
/// </summary>
public interface ICatalogueCopy
{
    string Description { get; set; }

    string? Language { get; set; }

    string? SearchTerms { get; set; }
}
