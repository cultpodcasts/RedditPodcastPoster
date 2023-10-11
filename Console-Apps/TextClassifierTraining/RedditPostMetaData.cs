namespace TextClassifierTraining;

public class RedditPostMetaData
{
    public string SpotifyId { get; set; }
    public string YouTubeId { get; set; }
    public long? AppleId { get; set; }
    public string Flair { get; set; }

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(SpotifyId))
        {
            return $"SpotifyId={SpotifyId}";
        }

        if (!string.IsNullOrWhiteSpace(YouTubeId))
        {
            return $"YouTubeId={YouTubeId}";
        }

        if (AppleId.HasValue)
        {
            return $"AppleId={AppleId.Value}";
        }

        return "Unknown Id";
    }
}