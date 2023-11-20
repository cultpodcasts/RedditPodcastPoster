using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class SubjectModel
{
    [JsonPropertyName("subjects")]
    public required IDictionary<string, string[]> TermSubjects { get; set; }

}