﻿using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class CosmosSelector
{
    protected CosmosSelector(Guid id, ModelType modelType)
    {
        Id = id;
        ModelType = modelType;
    }

    public string GetPartitionKey()
    {
        return ModelType.ToString();
    }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; }
}