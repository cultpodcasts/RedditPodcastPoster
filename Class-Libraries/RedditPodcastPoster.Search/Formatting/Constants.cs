namespace RedditPodcastPoster.Search.Formatting;

public static class Constants
{
    /// <summary>
    /// Indexed episode description cap (characters, including the ellipsis).
    /// Cap 50 fits the Free tier's 50 MB index quota. On 22 Oct 2026 the index reached
    /// 52.06 MB of 52.43 MB at cap 100 (88,279 documents, description about 29% of stored text),
    /// and catalogue-migrate search swaps were rejected with "Storage quota has been exceeded".
    /// Earlier, a 120-cap refresh on 25 Sep 2026 grew the same index from 46.64 MB to 63.66 MB.
    /// Changing the cap needs a search teardown and rebuild so existing documents shrink too.
    /// </summary>
    public const int DescriptionSize = 50;
}
