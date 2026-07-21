using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Discover;

public interface IDiscoveryResultConsoleLogger
{
    void DisplayEpisode(DiscoveryResult episode, ConsoleColor defaultColor);
}