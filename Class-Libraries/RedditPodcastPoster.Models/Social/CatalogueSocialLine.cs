namespace RedditPodcastPoster.Models.Social;

/// <summary>
/// Parent line for a social post. Podcast posting stays on its current template.
/// A film has no parent. TV and news name the show or organisation.
/// </summary>
public static class CatalogueSocialLine
{
    public static string? ParentLine(string? contentKind, string? parentName)
    {
        if (contentKind is "Film")
        {
            return null;
        }

        if (contentKind is "TvShowEpisode" or "NewsReport")
        {
            return string.IsNullOrWhiteSpace(parentName) ? null : parentName.Trim();
        }

        return string.IsNullOrWhiteSpace(parentName) ? null : parentName.Trim();
    }
}
