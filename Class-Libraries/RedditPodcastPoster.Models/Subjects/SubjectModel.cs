using System.Text.Json.Serialization;


namespace RedditPodcastPoster.Models.Subjects;

public class SubjectModel
{
    [JsonPropertyName("subjects")]
    public required IDictionary<string, string[]> TermSubjects { get; set; }

}