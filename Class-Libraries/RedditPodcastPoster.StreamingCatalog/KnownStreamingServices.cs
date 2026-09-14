using RedditPodcastPoster.AmazonPrime;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.BcVideo;
using RedditPodcastPoster.Channel4;
using RedditPodcastPoster.DiscoveryPlus;
using RedditPodcastPoster.DisneyPlus;
using RedditPodcastPoster.Fawesome;
using RedditPodcastPoster.FranceTv;
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
        DiscoveryPlusStreamingService.Registration,
        FranceTvStreamingService.Registration
    ];

    public static readonly string[] ImageCoalesceStreamingKeys =
        DeriveImageCoalesceStreamingKeys(All);

    /// <summary>
    /// Cover-art order is search-encode order with BBC iPlayer before Sounds.
    /// A new provider is added only to <see cref="All"/>.
    /// </summary>
    private static string[] DeriveImageCoalesceStreamingKeys(
        IReadOnlyList<IStreamingServiceRegistration> registrations)
    {
        var keys = registrations.Select(r => r.Descriptor.Key).ToArray();
        var sounds = Array.IndexOf(keys, StreamingServiceKeys.BbcSounds);
        var iplayer = Array.IndexOf(keys, StreamingServiceKeys.BbcIplayer);
        if (sounds >= 0 && iplayer >= 0 && iplayer > sounds)
        {
            (keys[sounds], keys[iplayer]) = (keys[iplayer], keys[sounds]);
        }

        return keys;
    }
}
