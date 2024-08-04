using System.Text.Json.Serialization;
using RedditPodcastPoster.UrlSubmission;
using static RedditPodcastPoster.UrlSubmission.SubmitResult;

namespace Api.Dtos;

public class SubmitUrlResponse
{
    [JsonPropertyName("success")]
    public SubmitUrlSuccessResponse? Success { get; private set; }

    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    private static SubmitItemResponse ToSubmitEpisodeResponse(SubmitResultState submitResultState)
    {
        return submitResultState switch
        {
            SubmitResultState.None => SubmitItemResponse.None,
            SubmitResultState.Created => SubmitItemResponse.Created,
            SubmitResultState.Enriched => SubmitItemResponse.Enriched,
            SubmitResultState.PodcastRemoved => SubmitItemResponse.Ignored,
            _ => throw new ArgumentException($"Unknown value '{submitResultState}'.", nameof(submitResultState))
        };
    }

    public static SubmitUrlResponse Successful(SubmitResult result)
    {
        return new SubmitUrlResponse
        {
            Success = new SubmitUrlSuccessResponse(
                ToSubmitEpisodeResponse(result.EpisodeResult),
                ToSubmitEpisodeResponse(result.PodcastResult),
                result.EpisodeId
            )
        };
    }

    public static SubmitUrlResponse Failure(string message = "")
    {
        return new SubmitUrlResponse {Error = message};
    }
}