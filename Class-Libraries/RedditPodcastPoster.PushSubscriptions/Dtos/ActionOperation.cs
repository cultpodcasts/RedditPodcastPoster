using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PushSubscriptions.Dtos;

public enum ActionOperation
{
    [JsonPropertyName("openWindow")]
    OpenWindow = 1,

    [JsonPropertyName("focusLastFocusedOrOpen")]
    FocusLastFocusedOrOpen,

    [JsonPropertyName("navigateLastFocusedOrOpen")]
    NavigateLastFocusedOrOpen,

    [JsonPropertyName("sendRequest")]
    SendRequest
}