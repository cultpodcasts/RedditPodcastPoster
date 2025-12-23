using System.Net;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models;

public class PostModel
{
    public PostModel(
        string podcastName,
        string podcastTitleRegex,
        string podcastDescriptionRegex,
        IEnumerable<EpisodePost> episodes,
        Service? podcastPrimaryPostService,
        string[] podcastKnownTerms,
        string[] subjectKnownTerms)
    {
        var firstEpisode = episodes.First();

        Episodes = episodes;
        PodcastName = podcastName;
        IsBundledPost = episodes.Count() > 1;
        HasYouTubeUrl = firstEpisode.YouTube != null;
        Spotify = firstEpisode.Spotify;
        Apple = firstEpisode.Apple;
        YouTube = firstEpisode.YouTube;
        BBC = firstEpisode.BBC;
        InternetArchive = firstEpisode.InternetArchive;
        ReleaseDate = firstEpisode.Release;
        Published = firstEpisode.Published;
        EpisodeLength = firstEpisode.Duration;
        EpisodeDescription = WebUtility.HtmlDecode(firstEpisode.Description);
        EpisodeTitle = WebUtility.HtmlDecode(firstEpisode.Title);
        Id = firstEpisode.Id;
        Subjects = firstEpisode.Subjects;
        PodcastKnownTerms = podcastKnownTerms;
        SubjectKnownTerms = subjectKnownTerms;

        if (!string.IsNullOrWhiteSpace(podcastTitleRegex))
        {
            TitleRegex = new Regex(podcastTitleRegex, Podcast.TitleFlags);
        }

        if (!string.IsNullOrWhiteSpace(podcastDescriptionRegex))
        {
            DescriptionRegex = new Regex(podcastDescriptionRegex, Podcast.DescriptionFlags);
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

        if (podcastPrimaryPostService.HasValue)
        {
            Link = podcastPrimaryPostService switch
            {
                Service.Apple => firstEpisode.Apple,
                Service.Spotify => firstEpisode.Spotify,
                Service.YouTube => firstEpisode.YouTube,
                _ => Link
            };
        }

        Link ??= firstEpisode.YouTube ?? firstEpisode.Spotify ??
            firstEpisode.Apple ?? firstEpisode.InternetArchive ?? firstEpisode.BBC;
    }

    public DateTime Published { get; init; }
    public IEnumerable<EpisodePost> Episodes { get; set; }
    public bool IsBundledPost { get; init; }
    public Regex? TitleRegex { get; init; }
    public Regex? DescriptionRegex { get; init; }
    public bool HasYouTubeUrl { get; init; }
    public Uri? Spotify { get; init; }
    public Uri? Apple { get; init; }
    public Uri? YouTube { get; init; }
    public Uri? BBC { get; init; }
    public Uri? InternetArchive { get; init; }
    public IEnumerable<int> BundledPartNumbers { get; set; } = [];
    public string ReleaseDate { get; init; }
    public string EpisodeLength { get; init; }
    public string EpisodeDescription { get; init; }
    public string PodcastName { get; init; }
    public string EpisodeTitle { get; init; }
    public string[] Subjects { get; init; }
    public string Id { get; init; }
    public Uri? Link { get; init; }
    public string[] SubjectKnownTerms { get; init; }
    public string[] PodcastKnownTerms { get; init; }
}