using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastFactory
{
    private static readonly Regex Alphanumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);

    public Podcast Create(string podcastName)
    {

        var alphanumerics = Alphanumerics.Replace(podcastName, "");
        var removedSpacing = alphanumerics.Replace("  ", "");
        var fileKey= removedSpacing.Replace(" ", "_").ToLower();

        return new Podcast {Name = podcastName, FileKey = fileKey, Id = Guid.NewGuid()};

    }
}