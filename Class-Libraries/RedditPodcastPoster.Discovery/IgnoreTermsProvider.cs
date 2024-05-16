namespace RedditPodcastPoster.Discovery;

public class IgnoreTermsProvider : IIgnoreTermsProvider
{
    private static readonly string[] IgnoreTerms =
    {
        "cult of the lamb".ToLower(),
        "cult of lamb".ToLower(),
        "COTL".ToLower(),
        "cult of the lab".ToLower(),
        "Cult of the Lamp".ToLower(),
        "Cult of the Lumb".ToLower(),
        "Blue Oyster Cult".ToLower(),
        "Blue Öyster Cult".ToLower(),
        "Living Colour".ToLower(),
        "She Sells Sanctuary".ToLower(),
        "Far Cry".ToLower()
    };

    public IEnumerable<string> GetIgnoreTerms()
    {
        return IgnoreTerms;
    }
}