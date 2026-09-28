using RedditPodcastPoster.Models.ContentKinds;

namespace RedditPodcastPoster.Models.Social;

/// <summary>
/// Parent line for a social post. Podcast posting stays on its current template.
/// A film has no parent. An episode, TV episode, and news report name the show or organisation.
/// </summary>
public static class CatalogueSocialLine
{
    public static string? ParentLine(ContentKind? contentKind, string? parentName)
    {
        if (contentKind is ContentKind.Film)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(parentName) ? null : parentName.Trim();
    }
}
