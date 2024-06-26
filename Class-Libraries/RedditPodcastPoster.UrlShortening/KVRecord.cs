﻿using System.Text.Json.Serialization;

namespace RedditPodcastPoster.UrlShortening;

public class KVRecord
{
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
}