using System.Globalization;
using System.Runtime.Serialization;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

[DataContract]
public class PodcastEpisode
{
    [DataMember(Name = "releaseDate")]
    public string ReleaseIso { get; set; }

    public DateTime Release => DateTime.Parse(ReleaseIso, null, DateTimeStyles.RoundtripKind);

    [DataMember(Name = "trackExplicitness")]
    public string ExplicitStr { get; set; }

    public bool Explicit => ExplicitStr == "explicit";

    [DataMember(Name = "description")]
    public string Description { get; set; }

    [DataMember(Name = "trackName")]
    public string Title { get; set; }

    [DataMember(Name = "trackViewUrl")]
    public Uri Url { get; set; }

    [DataMember(Name = "trackTimeMillis")]
    public long LengthMs { get; set; }

    public TimeSpan Duration => TimeSpan.FromMilliseconds(LengthMs);

    [DataMember(Name = "wrapperType")]
    public string WrapperType { get; set; }

    public bool IsEpisode => WrapperType == "podcastEpisode";

    [DataMember(Name= "trackId")]
    public long Id { get; set; }
}