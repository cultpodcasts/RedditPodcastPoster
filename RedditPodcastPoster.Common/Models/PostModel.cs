using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Common.Models;

public class PostModel
{
    public PostModel(PodcastPost podcastPost)
    {
        PodcastPost = podcastPost;
        var episodes = podcastPost.Episodes;
        var firstEpisode = episodes.First();

        Episodes = podcastPost.Episodes;
        PodcastName = podcastPost.Name;

        IsBundledPost = PodcastPost.Episodes.Count() > 1;

        EpisodePost = firstEpisode;
        HasYouTubeUrl = firstEpisode.YouTube != null;
        Spotify = firstEpisode.Spotify;
        Apple = firstEpisode.Apple;
        YouTube = firstEpisode.YouTube;
        ReleaseDate = firstEpisode.Release;
        EpisodeLength = firstEpisode.Duration;
        EpisodeDescription = firstEpisode.Description;
        EpisodeTitle = firstEpisode.Title;
        Id = firstEpisode.Id;

        if (!string.IsNullOrWhiteSpace(podcastPost.TitleRegex))
        {
            TitleRegex = new Regex(podcastPost.TitleRegex);
        }

        if (!string.IsNullOrWhiteSpace(podcastPost.DescriptionRegex))
        {
            DescriptionRegex = new Regex(podcastPost.DescriptionRegex);
        }

        if (TitleRegex != null)
        {
            var bundledPartNumbers = new List<int>();
            foreach (var episode in episodes)
            {
                var parsed = int.TryParse(TitleRegex.Match(episode.Title)?.Result("${partnumber}"), out var partNumber);
                if (parsed)
                {
                    bundledPartNumbers.Add(partNumber);
                }
            }

            BundledPartNumbers = bundledPartNumbers;
        }

        Link = firstEpisode.YouTube ?? firstEpisode.Spotify ?? firstEpisode.Apple ?? null;
    }

    public IEnumerable<EpisodePost> Episodes { get; set; }
    public bool IsBundledPost { get; init; }
    public PodcastPost PodcastPost { get; init; }
    private EpisodePost EpisodePost { get; }
    public Regex? TitleRegex { get; init; }
    public Regex? DescriptionRegex { get; init; }
    public bool HasYouTubeUrl { get; init; }
    public Uri? Spotify { get; init; }
    public Uri? Apple { get; init; }
    public Uri? YouTube { get; init; }
    public IEnumerable<int> BundledPartNumbers { get; set; }
    public string ReleaseDate { get; init; }
    public string EpisodeLength { get; init; }
    public string EpisodeDescription { get; init; }
    public string PodcastName { get; init; }
    public string EpisodeTitle { get; init; }
    public string Id { get; init; }
    public Uri? Link { get; init; }
}