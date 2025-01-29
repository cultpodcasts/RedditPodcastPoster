using System.Text.Json.Serialization;
using RedditPodcastPoster.UrlSubmission;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Dtos;

public class SubmitUrlResponse
{
    [JsonPropertyName("success")]
    public SubmitUrlSuccessResponse? Success { get; private set; }

    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    public static SubmitUrlResponse Successful(SubmitResult result)
    {
        return new SubmitUrlResponse
        {
            Success = new SubmitUrlSuccessResponse(
                ToSubmitEpisodeResponse(result.EpisodeResult),
                ToSubmitEpisodeResponse(result.PodcastResult),
                ToSubmitEpisodeDetails(result.SubmitEpisodeDetails),
                result.EpisodeId
            )
        };
    }

    public static SubmitUrlResponse Failure(string message = "")
    {
        return new SubmitUrlResponse {Error = message};
    }

    private static SubmitItemResponse ToSubmitEpisodeResponse(SubmitResultState submitResultState)
    {
        return submitResultState switch
        {
            SubmitResultState.None => SubmitItemResponse.None,
            SubmitResultState.Created => SubmitItemResponse.Created,
            SubmitResultState.Enriched => SubmitItemResponse.Enriched,
            SubmitResultState.PodcastRemoved => SubmitItemResponse.Ignored,
            SubmitResultState.EpisodeAlreadyExists => SubmitItemResponse.EpisodeAlreadyExists,
            _ => throw new ArgumentException($"Unknown value '{submitResultState}'.", nameof(submitResultState))
        };
    }

    private static SubmitEpisodeDetails? ToSubmitEpisodeDetails(
        RedditPodcastPoster.UrlSubmission.Models.SubmitEpisodeDetails? resultSubmitEpisodeDetails)
    {
        if (resultSubmitEpisodeDetails != null)
        {
            return new SubmitEpisodeDetails(resultSubmitEpisodeDetails.Spotify,
                resultSubmitEpisodeDetails.Apple,
                resultSubmitEpisodeDetails.YouTube,
                resultSubmitEpisodeDetails.BBC,
                resultSubmitEpisodeDetails.InternetArchive,
                resultSubmitEpisodeDetails.Subjects ?? []);
        }

        return null;
    }
}