using RedditPodcastPoster.AmazonPrime;
using RedditPodcastPoster.AppleTvPlus;
using RedditPodcastPoster.Ard;
using RedditPodcastPoster.Arte;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.CanalPlus;
using RedditPodcastPoster.BcVideo;
using RedditPodcastPoster.Channel4;
using RedditPodcastPoster.DiscoveryPlus;
using RedditPodcastPoster.DisneyPlus;
using RedditPodcastPoster.Fawesome;
using RedditPodcastPoster.FranceTv;
using RedditPodcastPoster.HboMax;
using RedditPodcastPoster.Hulu;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.Itvx;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix;
using RedditPodcastPoster.ParamountPlus;
using RedditPodcastPoster.Peacock;
using RedditPodcastPoster.PlayRts;
using RedditPodcastPoster.PlaySuisse;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Tubi;
using RedditPodcastPoster.TvnzPlus;
using RedditPodcastPoster.Vimeo;
using RedditPodcastPoster.Zdf;

namespace RedditPodcastPoster.StreamingCatalog;

/// <summary>
/// Composed registrations in <see cref="StreamingService"/> declaration order.
/// Metadata (display/icon/hosts) comes from enum attributes; plugins supply match/compact only.
/// Image coalesce lists iPlayer before Sounds (cover-art preference), unlike search encode.
/// </summary>
public static class KnownStreamingServices
{
    public static readonly IReadOnlyList<IStreamingServiceRegistration> All =
    [
        BbcSoundsStreamingService.Registration,
        BbcIplayerStreamingService.Registration,
        InternetArchiveStreamingService.Registration,
        VimeoStreamingService.Registration,
        NetflixStreamingService.Registration,
        AmazonPrimeStreamingService.Registration,
        ParamountPlusStreamingService.Registration,
        HboMaxStreamingService.Registration,
        PlaySuisseStreamingService.Registration,
        PlayRtsStreamingService.Registration,
        TvnzPlusStreamingService.Registration,
        ItvxStreamingService.Registration,
        Channel4StreamingService.Registration,
        FawesomeStreamingService.Registration,
        DisneyPlusStreamingService.Registration,
        BcVideoStreamingService.Registration,
        TubiStreamingService.Registration,
        DiscoveryPlusStreamingService.Registration,
        FranceTvStreamingService.Registration,
        ArteStreamingService.Registration,
        HuluStreamingService.Registration,
        PeacockStreamingService.Registration,
        AppleTvPlusStreamingService.Registration,
        ZdfStreamingService.Registration,
        ArdStreamingService.Registration,
        CanalPlusStreamingService.Registration
    ];

    public static readonly string[] ImageCoalesceStreamingKeys = StreamingServiceWire.ImageCoalesceKeys;
}
