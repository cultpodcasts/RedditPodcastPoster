using RedditPodcastPoster.Models;

namespace Discover;

public class DiscoveryResultConsoleLogger : IDiscoveryResultConsoleLogger
{
    public void DisplayEpisode(DiscoveryResult episode, ConsoleColor defaultColor)
    {
        Console.WriteLine(new string('-', 40));
        if (episode.Urls.Apple != null)
        {
            Console.WriteLine(episode.Urls.Apple);
        }

        if (episode.Urls.Spotify != null)
        {
            Console.WriteLine(episode.Urls.Spotify);
        }

        if (episode.Urls.YouTube != null)
        {
            Console.WriteLine(episode.Urls.YouTube);
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(episode.EpisodeName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(episode.ShowName);
        if (!string.IsNullOrWhiteSpace(episode.Description))
        {
            var description = episode.Description;
            var min = Math.Min(description.Length, 200);
            Console.ForegroundColor = defaultColor;
            Console.WriteLine(description[..min]);
        }

        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine(episode.Released.ToString("g"));
        if (episode.Length != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(episode.Length);
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        foreach (var subject in episode.Subjects)
        {
            Console.WriteLine(subject);
        }

        if (episode.YouTubeViews.HasValue || episode.YouTubeChannelMembers.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            const string unknown = "Unknown";
            Console.WriteLine(
                $"YouTubeViews: {(episode.YouTubeViews.HasValue ? episode.YouTubeViews.Value : unknown)}, Members: {(episode.YouTubeChannelMembers.HasValue ? episode.YouTubeChannelMembers.Value : unknown)}");
        }

        Console.ForegroundColor = defaultColor;
        Console.WriteLine();
    }
}