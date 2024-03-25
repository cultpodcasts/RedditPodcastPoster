using System.Net;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models;

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

        HasYouTubeUrl = firstEpisode.YouTube != null;
        Spotify = firstEpisode.Spotify;
        Apple = firstEpisode.Apple;
        YouTube = firstEpisode.YouTube;
        ReleaseDate = firstEpisode.Release;
        Published = firstEpisode.Published;
        EpisodeLength = firstEpisode.Duration;
        EpisodeDescription = WebUtility.HtmlDecode(firstEpisode.Description);
        EpisodeTitle = WebUtility.HtmlDecode(firstEpisode.Title);
        Id = firstEpisode.Id;
        Subjects = firstEpisode.Subjects;

        if (!string.IsNullOrWhiteSpace(podcastPost.TitleRegex))
        {
            TitleRegex = new Regex(podcastPost.TitleRegex);
        }

        if (!string.IsNullOrWhiteSpace(podcastPost.DescriptionRegex))
        {
            DescriptionRegex = new Regex(podcastPost.DescriptionRegex, RegexOptions.Singleline);
        }

        if (TitleRegex != null)
        {
            var bundledPartNumbers = new List<int>();
            foreach (var episode in episodes)
            {
                var match = TitleRegex.Match(episode.Title);
                if (match.Success)
                {
                    var parsed = int.TryParse(match?.Result("${partnumber}"), out var partNumber);
                    if (parsed)
                    {
                        bundledPartNumbers.Add(partNumber);
                    }
                }
            }

            BundledPartNumbers = bundledPartNumbers;
        }

        if (podcastPost.PodcastPrimaryPostService.HasValue)
        {
            Link = podcastPost.PodcastPrimaryPostService switch
            {
                Service.Apple => firstEpisode.Apple,
                Service.Spotify => firstEpisode.Spotify,
                Service.YouTube => firstEpisode.YouTube,
                _ => Link
            };
        }

        Link ??= firstEpisode.YouTube ?? firstEpisode.Spotify ?? firstEpisode.Apple ?? null;
    }

    public DateTime Published { get; init; }
    public IEnumerable<EpisodePost> Episodes { get; set; }
    public bool IsBundledPost { get; init; }
    public PodcastPost PodcastPost { get; init; }
    public Regex? TitleRegex { get; init; }
    public Regex? DescriptionRegex { get; init; }
    public bool HasYouTubeUrl { get; init; }
    public Uri? Spotify { get; init; }
    public Uri? Apple { get; init; }
    public Uri? YouTube { get; init; }
    public IEnumerable<int> BundledPartNumbers { get; set; } = Enumerable.Empty<int>();
    public string ReleaseDate { get; init; }
    public string EpisodeLength { get; init; }
    public string EpisodeDescription { get; init; }
    public string PodcastName { get; init; }
    public string EpisodeTitle { get; init; }
    public IList<string> Subjects { get; init; }
    public string Id { get; init; }
    public Uri? Link { get; init; }
}