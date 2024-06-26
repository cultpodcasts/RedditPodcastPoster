﻿using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitResponseItem
{
    [JsonPropertyName("discoveryItemId")]
    public Guid DiscoveryItemId { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}