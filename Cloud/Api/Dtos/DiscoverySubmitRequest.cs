﻿using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitRequest
{
    [JsonPropertyName("ids")]
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    [JsonPropertyName("resultIds")]
    public Guid[] ResultIds { get; set; } = [];
}