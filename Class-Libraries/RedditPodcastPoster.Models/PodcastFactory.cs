﻿using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models;

public class PodcastFactory
{
    private static readonly Regex Alphanumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);

    public Podcast Create(string podcastName)
    {

        var alphanumerics = Alphanumerics.Replace(podcastName, "");
        var removedSpacing = alphanumerics.Replace("  ", "");
        var fileKey= removedSpacing.Replace(" ", "_").ToLower();

        return new Podcast(Guid.NewGuid()) {Name = podcastName, FileKey = fileKey};

    }
}