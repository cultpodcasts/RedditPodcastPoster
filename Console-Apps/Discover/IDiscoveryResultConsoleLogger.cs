﻿using RedditPodcastPoster.Models;

namespace Discover;

public interface IDiscoveryResultConsoleLogger
{
    void DisplayEpisode(DiscoveryResult episode, ConsoleColor defaultColor);
}