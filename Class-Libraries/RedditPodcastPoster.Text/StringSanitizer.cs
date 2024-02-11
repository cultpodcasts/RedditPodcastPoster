namespace RedditPodcastPoster.Text;

public static class StringSanitizer 
{
    public static string FixEntitles(this string title)
    {
        title = title.Replace("&apos;", "'");
        title = title.Replace("&quot;", "\"");
        title = title.Replace("&amp;", "&");
        title = title.Replace("&#8217;", "'");
        title = title.Replace("&#39;", "'");
        title = title.Replace("\n", " ");
        title = title.Replace("\r", " ");
        title = title.Replace("&lt;", "<");
        title = title.Replace("&gt;", ">");
        title = title.Replace("&#34;", "\"");
        return title;
    }
}