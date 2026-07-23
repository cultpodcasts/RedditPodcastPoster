namespace RedditPodcastPoster.Text.Sanitisers;

public static class StringSanitizer 
{
    public static string FixEntitles(this string title)
    {
        title = title.Replace("&apos;", "'");
        title = title.Replace("&quot;", "\"");
        title = title.Replace("&amp;", "&");
        title = title.Replace("&#8217;", "'");
        title = title.Replace("&#39;", "'");
        // Typographic / lookalike apostrophes: ToTitleCase capitalises the following letter (e.g. ’s → ’S)
        title = title.Replace('\u2018', '\''); // left single quotation mark
        title = title.Replace('\u2019', '\''); // right single quotation mark
        title = title.Replace('\u02BC', '\''); // modifier letter apostrophe
        title = title.Replace('\u2032', '\''); // prime
        title = title.Replace('\u00B4', '\''); // acute accent
        title = title.Replace("\n", " ");
        title = title.Replace("\r", " ");
        title = title.Replace("&lt;", "<");
        title = title.Replace("&gt;", ">");
        title = title.Replace("&#34;", "\"");
        title = title.Replace("&#64;", "@");
        title = title.Replace("&#61;", "=");
        return title;
    }
}
