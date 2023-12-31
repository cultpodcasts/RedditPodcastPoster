﻿using System.Runtime.Serialization;

namespace RedditPodcastPoster.PodcastServices.Apple;

[DataContract]
public class PodcastEpisodeListResult
{
    [DataMember(Name = "resultCount")]
    public int Count { get; set; }

    [DataMember(Name = "results")]
    public List<PodcastEpisode> Episodes { get; set; } = new();
}