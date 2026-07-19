using System.Text.Json.Serialization;
using RedditPodcastPoster.People.Models;
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
                result.Episode?.Id,
                result.Podcast?.Id
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
                resultSubmitEpisodeDetails.Subjects ?? [],
                resultSubmitEpisodeDetails.People ?? [],
                (resultSubmitEpisodeDetails.GuestSuggestions ?? [])
                .Select(ToPersonMatchDto)
                .ToArray());
        }

        return null;
    }

    private static PersonMatchDto ToPersonMatchDto(PersonMatch match)
    {
        return new PersonMatchDto
        {
            Person = new Person
            {
                Id = match.Person.Id,
                Name = match.Person.Name,
                TwitterHandle = match.Person.TwitterHandle,
                BlueskyHandle = match.Person.BlueskyHandle
            },
            MatchResults = match.MatchResults
                .Select(x => new PersonMatchResultDto { Term = x.Term, Matches = x.Matches })
                .ToArray()
        };
    }
}