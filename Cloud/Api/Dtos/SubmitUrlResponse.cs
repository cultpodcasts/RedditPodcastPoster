using System.Text.Json.Serialization;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.UrlSubmission.Languages;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Dtos;

public class SubmitUrlResponse
{
    [JsonPropertyName("success")]
    public SuccessBody? Success { get; private set; }

    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    /// <summary>Set when nothing was created because the detected language is not supported yet.</summary>
    [JsonPropertyName("languageDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LanguageDecisionBody? LanguageDecision { get; private set; }

    /// <summary>Set when nothing was created because authority search matched several programmes.</summary>
    [JsonPropertyName("identityDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IdentityDecisionBody? IdentityDecision { get; private set; }

    /// <summary>The caller chose cancel; nothing was created.</summary>
    [JsonPropertyName("cancelled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Cancelled { get; private set; }

    public static SubmitUrlResponse Successful(SubmitResult result)
    {
        return new SubmitUrlResponse
        {
            Success = new SuccessBody(
                ToItemState(result.EpisodeResult),
                ToItemState(result.PodcastResult),
                ToEpisodeDetails(result.SubmitEpisodeDetails),
                result.Episode?.Id,
                result.Podcast?.Id,
                result.ContentKind,
                result.PlayableId,
                result.DefaultLanguageFromDetection)
        };
    }

    public static SubmitUrlResponse DecisionRequired(CatalogueLanguageHold hold) =>
        new() { LanguageDecision = ToDecision(hold) };

    public static SubmitUrlResponse IdentityDecisionRequired(CatalogueIdentityHold hold) =>
        new() { IdentityDecision = ToIdentityDecision(hold) };

    public static SubmitUrlResponse CancelledResponse() => new() { Cancelled = true };

    public static SubmitUrlResponse Failure(string message = "")
    {
        return new SubmitUrlResponse { Error = message };
    }

    private static LanguageDecisionBody ToDecision(CatalogueLanguageHold hold) =>
        new()
        {
            Code = hold.Code,
            Name = hold.Name,
            Signals = hold.Signals.Select(x => new LanguageSignalBody
            {
                Field = x.Field,
                Label = x.Label,
                Excerpt = x.Excerpt,
                Reason = x.Reason
            }).ToArray()
        };

    private static IdentityDecisionBody ToIdentityDecision(CatalogueIdentityHold hold) =>
        new()
        {
            Target = hold.Target,
            Query = hold.Query,
            Candidates = hold.Candidates.Select(x => new IdentityCandidateBody
            {
                Key = x.Key,
                Title = x.Title,
                Year = x.Year,
                Kind = x.Kind,
                Source = x.Source,
                TmdbId = x.TmdbId,
                TvdbId = x.TvdbId,
                PageUrl = x.PageUrl?.ToString()
            }).ToArray()
        };

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
            resultSubmitEpisodeDetails.ExtraServiceKeys ?? [],
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
                .Select(x => new GuestSuggestion.MatchResult { Term = x.Term, Matches = x.Matches })
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
        Guid? podcastId,
        string? contentKind = null,
        Guid? playableId = null,
        bool defaultLanguageFromDetection = false)
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

        [JsonPropertyName("contentKind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContentKind { get; private set; } = contentKind;

        [JsonPropertyName("playableId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? PlayableId { get; private set; } = playableId;

        [JsonPropertyName("episodeDetails")]
        public EpisodeDetails? EpisodeDetails { get; private set; } = submitEpisodeDetails;

        /// <summary>True when this create stored a detected non-English default language on a new parent.</summary>
        [JsonPropertyName("defaultLanguageFromDetection")]
        public bool DefaultLanguageFromDetection { get; private set; } = defaultLanguageFromDetection;
    }

    public class LanguageDecisionBody
    {
        [JsonPropertyName("code")]
        public required string Code { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("signals")]
        public required LanguageSignalBody[] Signals { get; set; }
    }

    public class IdentityDecisionBody
    {
        /// <summary><c>Film</c>, <c>TvShow</c>, or <c>TvShowEpisode</c>.</summary>
        [JsonPropertyName("target")]
        public required string Target { get; set; }

        [JsonPropertyName("query")]
        public required string Query { get; set; }

        [JsonPropertyName("candidates")]
        public required IdentityCandidateBody[] Candidates { get; set; }
    }

    public class IdentityCandidateBody
    {
        /// <summary>Opaque key to send back as <c>selectedIdentityKey</c> with <c>identityDecision: select</c>.</summary>
        [JsonPropertyName("key")]
        public required string Key { get; set; }

        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("year")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Year { get; set; }

        [JsonPropertyName("kind")]
        public required string Kind { get; set; }

        /// <summary><c>tmdb</c> or <c>tvdb</c>.</summary>
        [JsonPropertyName("source")]
        public required string Source { get; set; }

        [JsonPropertyName("tmdbId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TmdbId { get; set; }

        [JsonPropertyName("tvdbId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? TvdbId { get; set; }

        [JsonPropertyName("pageUrl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PageUrl { get; set; }
    }

    public class LanguageSignalBody
    {
        [JsonPropertyName("field")]
        public required string Field { get; set; }

        [JsonPropertyName("label")]
        public required string Label { get; set; }

        [JsonPropertyName("excerpt")]
        public required string Excerpt { get; set; }

        [JsonPropertyName("reason")]
        public required string Reason { get; set; }
    }

    public class EpisodeDetails(
        bool spotify,
        bool apple,
        bool youTube,
        string[]? extraServiceKeys,
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

        [JsonPropertyName("extraServiceKeys")]
        public string[]? ExtraServiceKeys { get; private set; } = extraServiceKeys;

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
        public required MatchResult[] MatchResults { get; set; }

        public class MatchResult
        {
            [JsonPropertyName("term")]
            public required string Term { get; set; }

            [JsonPropertyName("matches")]
            public required int Matches { get; set; }
        }
    }
}
