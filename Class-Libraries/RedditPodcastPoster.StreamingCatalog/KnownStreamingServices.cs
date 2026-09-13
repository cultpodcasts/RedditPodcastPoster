using RedditPodcastPoster.AmazonPrime;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.BcVideo;
using RedditPodcastPoster.Channel4;
using RedditPodcastPoster.DiscoveryPlus;
using RedditPodcastPoster.DisneyPlus;
using RedditPodcastPoster.Fawesome;
using RedditPodcastPoster.HboMax;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.Itvx;
using RedditPodcastPoster.Netflix;
using RedditPodcastPoster.ParamountPlus;
using RedditPodcastPoster.PlayRts;
using RedditPodcastPoster.PlaySuisse;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Tubi;
using RedditPodcastPoster.TvnzPlus;
using RedditPodcastPoster.Vimeo;

namespace RedditPodcastPoster.StreamingCatalog;

/// <summary>
/// Contract order for <c>streamingServiceKeys</c> / search <c>svc</c> encoding.
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
        DiscoveryPlusStreamingService.Registration
    ];

    public static readonly string[] ImageCoalesceStreamingKeys =
    [
        StreamingServiceKeys.BbcIplayer,
        StreamingServiceKeys.BbcSounds,
        StreamingServiceKeys.InternetArchive,
        StreamingServiceKeys.Vimeo,
        StreamingServiceKeys.Netflix,
        StreamingServiceKeys.AmazonPrime,
        StreamingServiceKeys.ParamountPlus,
        StreamingServiceKeys.HboMax,
        StreamingServiceKeys.PlaySuisse,
        StreamingServiceKeys.PlayRts,
        StreamingServiceKeys.TvnzPlus,
        StreamingServiceKeys.Itvx,
        StreamingServiceKeys.Channel4,
        StreamingServiceKeys.Fawesome,
        StreamingServiceKeys.DisneyPlus,
        StreamingServiceKeys.BcVideo,
        StreamingServiceKeys.Tubi,
        StreamingServiceKeys.DiscoveryPlus
    ];
}
