using System.Text.Json.Serialization;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Dtos;

public class SubmitUrlResponse
{
    [JsonPropertyName("success")]
    public SuccessBody? Success { get; private set; }

    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    public static SubmitUrlResponse Successful(SubmitResult result)
    {
        return new SubmitUrlResponse
        {
            Success = new SuccessBody(
                ToItemState(result.EpisodeResult),
                ToItemState(result.PodcastResult),
                ToEpisodeDetails(result.SubmitEpisodeDetails),
                result.Episode?.Id,
                result.Podcast?.Id)
        };
    }

    public static SubmitUrlResponse Failure(string message = "")
    {
        return new SubmitUrlResponse { Error = message };
    }

    private static ItemState ToItemState(SubmitResultState submitResultState)
    {
        return submitResultState switch
        {
            SubmitResultState.None => ItemState.None,
            SubmitResultState.Created => ItemState.Created,
            SubmitResultState.Enriched => ItemState.Enriched,
            SubmitResultState.PodcastRemoved => ItemState.Ignored,
            SubmitResultState.EpisodeAlreadyExists => ItemState.EpisodeAlreadyExists,
            _ => throw new ArgumentException($"Unknown value '{submitResultState}'.", nameof(submitResultState))
        };
    }

    private static EpisodeDetails? ToEpisodeDetails(
        RedditPodcastPoster.UrlSubmission.Models.SubmitEpisodeDetails? resultSubmitEpisodeDetails)
    {
        if (resultSubmitEpisodeDetails == null)
        {
            return null;
        }

        return new EpisodeDetails(
            resultSubmitEpisodeDetails.Spotify,
            resultSubmitEpisodeDetails.Apple,
            resultSubmitEpisodeDetails.YouTube,
            resultSubmitEpisodeDetails.BBC,
            resultSubmitEpisodeDetails.InternetArchive,
            resultSubmitEpisodeDetails.Subjects ?? [],
            (resultSubmitEpisodeDetails.People ?? [])
            .Select(x => x.Person.Name)
            .ToArray(),
            (resultSubmitEpisodeDetails.GuestSuggestions ?? [])
            .Select(ToGuestSuggestion)
            .ToArray());
    }

    private static GuestSuggestion ToGuestSuggestion(PersonMatch match)
    {
        return new GuestSuggestion
        {
            Name = match.Person.Name,
            MatchResults = match.MatchResults
                .Select(x => new PersonMatchResultDto { Term = x.Term, Matches = x.Matches })
                .ToArray()
        };
    }

    public enum ItemState
    {
        None = 0,
        Created,
        Enriched,
        Ignored,
        EpisodeAlreadyExists
    }

    public class SuccessBody(
        ItemState episode,
        ItemState podcast,
        EpisodeDetails? submitEpisodeDetails,
        Guid? episodeId,
        Guid? podcastId)
    {
        [JsonPropertyName("episode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemState Episode { get; private set; } = episode;

        [JsonPropertyName("episodeId")]
        public Guid? EpisodeId { get; private set; } = episodeId;

        [JsonPropertyName("podcastId")]
        public Guid? PodcastId { get; private set; } = podcastId;

        [JsonPropertyName("podcast")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemState Podcast { get; private set; } = podcast;

        [JsonPropertyName("episodeDetails")]
        public EpisodeDetails? EpisodeDetails { get; private set; } = submitEpisodeDetails;
    }

    public class EpisodeDetails(
        bool spotify,
        bool apple,
        bool youTube,
        bool bbc,
        bool internetArchive,
        string[]? subjects,
        string[]? people,
        GuestSuggestion[]? guestSuggestions)
    {
        [JsonPropertyName("spotify")]
        public bool Spotify { get; private set; } = spotify;

        [JsonPropertyName("apple")]
        public bool Apple { get; private set; } = apple;

        [JsonPropertyName("youtube")]
        public bool YouTube { get; private set; } = youTube;

        [JsonPropertyName("bbc")]
        public bool BBC { get; private set; } = bbc;

        [JsonPropertyName("internetArchive")]
        public bool InternetArchive { get; private set; } = internetArchive;

        [JsonPropertyName("subjects")]
        public string[]? Subjects { get; private set; } = subjects;

        [JsonPropertyName("people")]
        public string[]? People { get; private set; } = people;

        [JsonPropertyName("guestSuggestions")]
        public GuestSuggestion[]? GuestSuggestions { get; private set; } = guestSuggestions;
    }

    public class GuestSuggestion
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("matchResults")]
        public required PersonMatchResultDto[] MatchResults { get; set; }
    }
}
