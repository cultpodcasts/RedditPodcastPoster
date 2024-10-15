using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace AddSubjectToSearchMatches;

public class SearchDocument
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastName")]
    public string? PodcastName { get; set; }

    [JsonPropertyName("episodeTitle")]
    public string EpisodeTitle { get; set; }

    [JsonPropertyName("episodeDescription")]
    public string EpisodeDescription { get; set; }

    [JsonPropertyName("subjects")]
    public string[] Subjects { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("release")]
    public DateTime? Release { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("apple")]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("spotify")]
    public Uri? Spotify { get; set; }

    public Episode ToEpisodeModel()
    {
        var length = !string.IsNullOrWhiteSpace(Duration) ? TimeSpan.Parse(Duration) : TimeSpan.Zero;
        return new Episode
        {
            Id = Id,
            Title = EpisodeTitle,
            Description = EpisodeDescription,
            Subjects = Subjects.ToList(),
            Length = length,
            Release = Release ?? DateTime.MinValue,
            Urls = new ServiceUrls
            {
                Apple = Apple,
                Spotify = Spotify,
                YouTube = YouTube
            },
            Explicit = Explicit ?? false
        };
    }
}