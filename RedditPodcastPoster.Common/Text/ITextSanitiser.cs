﻿using System.Text.RegularExpressions;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Text;

public interface ITextSanitiser
{
    string SanitiseTitle(PostModel postModel);
    string SanitisePodcastName(PostModel postModel);
    string SanitiseDescription(PostModel postModel);
    string SanitiseTitle(string episodeTitle, Regex? regex);
    string SanitisePodcastName(string podcastName);
    string SanitiseDescription(string episodeDescription, Regex? regex);
}