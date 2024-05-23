using System.Text.Json.Serialization;
using RedditPodcastPoster.UrlSubmission;
using static RedditPodcastPoster.UrlSubmission.SubmitResult;

namespace Api.Dtos;

public class SubmitUrlResponse
{
    public enum SubmitItemResponse
    {
        None = 0,
        CreatedEpisode,
        EnrichedEpisode
    }

    [JsonPropertyName("success")]
    public SubmitUrlSuccessResponse? Success { get; private set; }

    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    private static SubmitItemResponse ToSubmitEpisodeResponse(SubmitResultState submitResultState)
    {
        return submitResultState switch
        {
            SubmitResultState.None => SubmitItemResponse.None,
            SubmitResultState.Created => SubmitItemResponse.CreatedEpisode,
            SubmitResultState.Enriched => SubmitItemResponse.EnrichedEpisode,
            _ => throw new ArgumentException($"Unknown value '{submitResultState}'.", nameof(submitResultState))
        };
    }

    public static SubmitUrlResponse Successful(SubmitResult result)
    {
        return new SubmitUrlResponse
        {
            Success = new SubmitUrlSuccessResponse(
                ToSubmitEpisodeResponse(result.EpisodeResult),
                ToSubmitEpisodeResponse(result.PodcastResult))
        };
    }

    public static SubmitUrlResponse Failure(string message = "")
    {
        return new SubmitUrlResponse {Error = message};
    }

    public class SubmitUrlSuccessResponse(SubmitItemResponse episode, SubmitItemResponse podcast)
    {
        [JsonPropertyName("episode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubmitItemResponse Episode { get; private set; } = episode;

        [JsonPropertyName("podcast")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubmitItemResponse Podcast { get; private set; } = podcast;
    }
}