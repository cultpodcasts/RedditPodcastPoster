﻿using System.Text.Json.Serialization;
using Api.Dtos.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Converters;

namespace Api.Dtos;

public class DiscoveryResponseItem
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(10)]
    public DiscoveryResultUrls Urls { get; set; } = new();

    [JsonPropertyName("episodeName")]
    [JsonPropertyOrder(20)]
    public string? EpisodeName { get; set; }

    [JsonPropertyName("showName")]
    [JsonPropertyOrder(30)]
    public string? ShowName { get; set; }

    [JsonPropertyName("episodeDescription")]
    [JsonPropertyOrder(40)]
    public string? Description { get; set; }

    [JsonPropertyName("released")]
    [JsonPropertyOrder(50)]
    public DateTime Released { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(60)]
    public TimeSpan? Length { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public IEnumerable<string> Subjects { get; set; } = [];

    [JsonPropertyName("youTubeViews")]
    [JsonPropertyOrder(80)]
    public ulong? YouTubeViews { get; set; }

    [JsonPropertyName("youTubeChannelMembers")]
    [JsonPropertyOrder(90)]
    public ulong? YouTubeChannelMembers { get; set; }

    [JsonPropertyName("imageUrl")]
    [JsonPropertyOrder(100)]
    public Uri? ImageUrl { get; set; }

    [JsonPropertyName("discoverService")]
    [JsonConverter(typeof(ItemConverterDecorator<JsonStringEnumConverter>))]
    [JsonPropertyOrder(110)]
    public DiscoverService[] Sources { get; set; } = [];

    [JsonPropertyName("enrichedTimeFromApple")]
    [JsonPropertyOrder(120)]
    public bool EnrichedTimeFromApple { get; set; }

    [JsonPropertyName("enrichedUrlFromSpotify")]
    [JsonPropertyOrder(122)]
    public bool EnrichedUrlFromSpotify { get; set; }

    [JsonPropertyName("matchingPodcasts")]
    [JsonPropertyOrder(130)]
    public MatchingPodcast[] MatchingPodcasts { get; set; } = [];
}