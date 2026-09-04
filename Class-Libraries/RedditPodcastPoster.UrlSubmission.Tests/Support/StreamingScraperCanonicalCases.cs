namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

/// <summary>
/// Canonical live streaming URLs exercised by <see cref="BusinessRules.UrlSubmission.StreamingScraperUrlMembershipLookupRules"/>.
/// Browse homepage/section discovery lives in <see cref="StreamingScraperBrowsePages"/>.
/// </summary>
/// <remarks>
/// <para>
/// Live HTTP Theories detect provider HTML drift. CI sets <c>SKIP_LIVE_STREAMING_SCRAPER_TESTS=1</c>
/// (see <c>.github/workflows/dotnet.yml</c> / <c>deploy.yml</c> test job) so Build stays fully mocked.
/// Run locally or on a nightly job without that env (or with value other than <c>1</c>) to exercise live scrapes.
/// </para>
/// <para>
/// Static cases pin exact <c>podcastName</c> expectations (series, films, one-offs).
/// Homepage/section pages harvest additional submit URLs under
/// <see cref="BusinessRules.UrlSubmission.StreamingScraperBrowsePageHarvestRules"/> (same live gate).
/// </para>
/// </remarks>
public static class StreamingScraperCanonicalCases
{
    public static TheoryData<StreamingScraperCanonicalCase> BbcSoundsCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.BbcSounds));

    public static TheoryData<StreamingScraperCanonicalCase> BbcIplayerCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.BbcIplayer));

    public static TheoryData<StreamingScraperCanonicalCase> NetflixCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.Netflix));

    public static TheoryData<StreamingScraperCanonicalCase> AmazonPrimeCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.AmazonPrime));

    public static TheoryData<StreamingScraperCanonicalCase> VimeoCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.Vimeo));

    public static TheoryData<StreamingScraperCanonicalCase> ItvxCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.Itvx));

    public static TheoryData<StreamingScraperCanonicalCase> Channel4Cases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.Channel4));

    public static TheoryData<StreamingScraperCanonicalCase> FawesomeCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.Fawesome));

    public static TheoryData<StreamingScraperCanonicalCase> ParamountPlusCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.ParamountPlus));

    public static TheoryData<StreamingScraperCanonicalCase> HboMaxCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.HboMax));

    public static TheoryData<StreamingScraperCanonicalCase> PlaySuisseCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.PlaySuisse));

    public static TheoryData<StreamingScraperCanonicalCase> TvnzPlusCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.TvnzPlus));

    public static TheoryData<StreamingScraperCanonicalCase> DisneyPlusCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.DisneyPlus));

    public static TheoryData<StreamingScraperCanonicalCase> DiscoveryPlusCases() =>
        new(All.Where(c => c.Provider == StreamingScraperProvider.DiscoveryPlus));

    public static IEnumerable<StreamingScraperCanonicalCase> All =>
    [
        // BBC Sounds — brand programmes (homepage harvest added You're Dead To Me / Young Again)
        Case(StreamingScraperProvider.BbcSounds, "desert-island-discs-jony-ive",
            "https://www.bbc.co.uk/sounds/play/m00289vf", "Desert Island Discs",
            "Brand episode with aod_tracks music segments"),
        Case(StreamingScraperProvider.BbcSounds, "desert-island-discs-nick-cave",
            "https://www.bbc.co.uk/sounds/play/m0027cgl", "Desert Island Discs",
            "Second Desert Island Discs brand episode"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-battle-valmy",
            "https://www.bbc.co.uk/sounds/play/m0026vs5", "In Our Time",
            "Long-running discussion series brand"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-cyrus",
            "https://www.bbc.co.uk/sounds/play/m0028tzc", "In Our Time",
            "Second In Our Time specimen"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-pollination",
            "https://www.bbc.co.uk/sounds/play/m0028jtx", "In Our Time",
            "Episode headline differs from programme brand"),
        Case(StreamingScraperProvider.BbcSounds, "youre-dead-to-me-1066",
            "https://www.bbc.co.uk/sounds/play/m0030lsq", "You're Dead to Me",
            "Harvested from Sounds homepage"),
        Case(StreamingScraperProvider.BbcSounds, "young-again-gloria-steinem",
            "https://www.bbc.co.uk/sounds/play/m0024f5g", "Young Again",
            "Harvested from Sounds homepage"),

        // BBC iPlayer — series + strand + one-off
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-space-babies",
            "https://www.bbc.co.uk/iplayer/episode/m001z8bz/doctor-who-season-1-1-space-babies", "Doctor Who",
            "Series episode; redux title is brand"),
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-devils-chord",
            "https://www.bbc.co.uk/iplayer/episode/m001z8c7/doctor-who-season-1-2-the-devils-chord", "Doctor Who",
            "Second Doctor Who series episode"),
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-rose-archive",
            "https://www.bbc.co.uk/iplayer/episode/b0074dlv/doctor-who-20052022-series-1-1-rose", "Doctor Who (2005–2022)",
            "Archive franchise title with en-dash"),
        Case(StreamingScraperProvider.BbcIplayer, "panorama-scams-ai",
            "https://www.bbc.co.uk/iplayer/episode/m002wwqx/panorama-scams-lies-and-ai", "Panorama",
            "Current-affairs strand edition"),
        Case(StreamingScraperProvider.BbcIplayer, "panorama-undercover-police",
            "https://www.bbc.co.uk/iplayer/episode/m002k7k6/panorama-undercover-in-the-police", "Panorama",
            "Investigative strand edition"),
        Case(StreamingScraperProvider.BbcIplayer, "dolly-parton-here-i-am",
            "https://www.bbc.co.uk/iplayer/episode/m000crhq/dolly-parton-here-i-am", null,
            "One-off from iPlayer home harvest; no distinct parent brand"),

        // Netflix — series catalogues + films
        Case(StreamingScraperProvider.Netflix, "stranger-things-catalogue",
            "https://www.netflix.com/title/80057281", "Stranger Things",
            "Series catalogue"),
        Case(StreamingScraperProvider.Netflix, "black-mirror-catalogue",
            "https://www.netflix.com/title/70264888", "Black Mirror",
            "Anthology series catalogue"),
        Case(StreamingScraperProvider.Netflix, "the-crown-catalogue",
            "https://www.netflix.com/title/80025678", "The Crown",
            "Period drama series catalogue"),
        Case(StreamingScraperProvider.Netflix, "dark-catalogue",
            "https://www.netflix.com/title/80100172", "Dark",
            "Series catalogue"),
        Case(StreamingScraperProvider.Netflix, "squid-game-catalogue",
            "https://www.netflix.com/title/81040344", "Squid Game",
            "Series catalogue"),
        Case(StreamingScraperProvider.Netflix, "money-heist-catalogue",
            "https://www.netflix.com/title/80192098", "Money Heist",
            "Series catalogue"),
        Case(StreamingScraperProvider.Netflix, "bridgerton-catalogue",
            "https://www.netflix.com/title/80232398", "Bridgerton",
            "Series catalogue"),
        Case(StreamingScraperProvider.Netflix, "when-they-see-us-catalogue",
            "https://www.netflix.com/title/80200549", "When They See Us",
            "Limited series catalogue"),
        Case(StreamingScraperProvider.Netflix, "bird-box-film",
            "https://www.netflix.com/title/80196789", null,
            "Standalone film"),
        Case(StreamingScraperProvider.Netflix, "the-irishman-film",
            "https://www.netflix.com/title/80175798", null,
            "Standalone film"),
        Case(StreamingScraperProvider.Netflix, "dont-look-up-film",
            "https://www.netflix.com/title/81252357", null,
            "Standalone film"),

        // Amazon Prime — seasons + films (storefront harvest)
        Case(StreamingScraperProvider.AmazonPrime, "the-boys-season-1",
            "https://www.primevideo.com/detail/0S1FYJ3LY9KTL9C7WFFAGA9F6F", "The Boys",
            "Season page"),
        Case(StreamingScraperProvider.AmazonPrime, "clarksons-farm-season-5",
            "https://www.primevideo.com/detail/0OT6JCWNTHGSU7KAL5EBV7UJ5P", "Clarkson's Farm",
            "Season page"),
        Case(StreamingScraperProvider.AmazonPrime, "rings-of-power-season-1",
            "https://www.primevideo.com/detail/0TUVXIO58IUNEPNBF8363Z7YGL",
            "The Lord of the Rings: The Rings of Power",
            "Season page"),
        Case(StreamingScraperProvider.AmazonPrime, "reacher-season-storefront",
            "https://www.primevideo.com/detail/0K16R3PLUFGC2JUE457C26O4OD", "Reacher",
            "Harvested from Prime storefront home"),
        Case(StreamingScraperProvider.AmazonPrime, "meet-the-owens-storefront",
            "https://www.primevideo.com/detail/0RGJBVSSHA7UYPVJ2HTLBZW2N8", "Meet The Owens",
            "Harvested from Prime storefront home"),
        Case(StreamingScraperProvider.AmazonPrime, "sterling-point-storefront",
            "https://www.primevideo.com/detail/0K94N2UBORZCLPIC46VTJQ093E", "Sterling Point",
            "Harvested from Prime storefront home"),
        Case(StreamingScraperProvider.AmazonPrime, "air-film",
            "https://www.primevideo.com/detail/0IKIGIXFRNQP5ZT74X0037B4X5", null,
            "Standalone film"),
        Case(StreamingScraperProvider.AmazonPrime, "manchester-by-the-sea-film",
            "https://www.primevideo.com/detail/0Q9AXD5XXKTFYLGE5ET1QXN4EJ", null,
            "Standalone film"),
        Case(StreamingScraperProvider.AmazonPrime, "practical-magic-film",
            "https://www.primevideo.com/detail/B00HJ8RC2K", null,
            "Standalone film from storefront harvest"),

        // Vimeo — publisher/author (incl. homepage harvest)
        Case(StreamingScraperProvider.Vimeo, "big-buck-bunny",
            "https://vimeo.com/1084537", "Blender",
            "Open channel upload via oEmbed author"),
        Case(StreamingScraperProvider.Vimeo, "the-city-limits",
            "https://vimeo.com/23237102", "Dominic",
            "Independent creator upload"),
        Case(StreamingScraperProvider.Vimeo, "the-mountain",
            "https://vimeo.com/22439234", "TSO Photography",
            "Time-lapse creator channel"),
        Case(StreamingScraperProvider.Vimeo, "travis-scott-clip",
            "https://vimeo.com/357274789", "Wesley Luyten",
            "User upload with personal channel name"),
        Case(StreamingScraperProvider.Vimeo, "cibc-corporate",
            "https://vimeo.com/104653183", "CIBC",
            "Corporate channel upload"),
        Case(StreamingScraperProvider.Vimeo, "vimeo-home-les-betes",
            "https://vimeo.com/1088909783", "Michael Granberry",
            "Harvested from Vimeo homepage"),
        Case(StreamingScraperProvider.Vimeo, "vimeo-home-canine-massage",
            "https://vimeo.com/1074471464", "Emma D. Miller",
            "Harvested from Vimeo homepage"),

        // ITVX — brand watch pages (often geo-walled outside the UK)
        Case(StreamingScraperProvider.Itvx, "love-island-brand",
            "https://www.itv.com/watch/love-island/2a3697", "Love Island",
            "ITVX series brand page; live scrape may reset outside the UK"),
        Case(StreamingScraperProvider.Itvx, "vera-brand",
            "https://www.itv.com/watch/vera/1a7314", "Vera",
            "ITVX drama brand page"),

        // Channel 4 — programme hubs + on-demand episode
        Case(StreamingScraperProvider.Channel4, "great-british-bake-off-hub",
            "https://www.channel4.com/programmes/the-great-british-bake-off", "The Great British Bake Off",
            "SSR programme hub; brandTitle is the series name"),
        Case(StreamingScraperProvider.Channel4, "countdown-on-demand",
            "https://www.channel4.com/programmes/countdown/on-demand/75051-091", "Countdown",
            "On-demand episode under a long-running series brand"),
        Case(StreamingScraperProvider.Channel4, "channel-4-news-hub",
            "https://www.channel4.com/programmes/channel-4-news", "Channel 4 News",
            "News strand brand is distinct from the Channel 4 platform publisher"),

        // Fawesome — AVOD film (homepage does not SSR deep links)
        Case(StreamingScraperProvider.Fawesome, "calla-lily-film",
            "https://fawesome.tv/movies/10527435/calla-lily", null,
            "Standalone film; ShowName must stay null"),

        // Paramount+
        Case(StreamingScraperProvider.ParamountPlus, "tulsa-king-show",
            "https://www.paramountplus.com/shows/tulsa-king/", "Tulsa King",
            "Series catalogue with og:type video.tv_show"),
        Case(StreamingScraperProvider.ParamountPlus, "strange-new-worlds-show",
            "https://www.paramountplus.com/shows/star-trek-strange-new-worlds/", "Star Trek: Strange New Worlds",
            "Series catalogue"),
        Case(StreamingScraperProvider.ParamountPlus, "top-gun-maverick-film",
            "https://www.paramountplus.com/movies/top-gun-maverick/", null,
            "Film path; ShowName must stay null"),

        // HBO Max / Max — often geo/auth walled
        Case(StreamingScraperProvider.HboMax, "last-of-us-show",
            "https://www.max.com/shows/the-last-of-us", "The Last of Us",
            "Max series page; live scrape may 404 outside a licensed region"),
        Case(StreamingScraperProvider.HboMax, "dune-film",
            "https://www.max.com/movies/dune", null,
            "Movie path; ShowName must stay null; live scrape may 404 outside a licensed region"),

        // Play Suisse — watch/detail numeric ids; homepage is marketing-only
        Case(StreamingScraperProvider.PlaySuisse, "play-suisse-watch",
            "https://www.playsuisse.ch/watch/2261604", null,
            "Numeric watch id; film/one-off expected unless JSON-LD TVSeries is present"),

        // TVNZ+
        Case(StreamingScraperProvider.TvnzPlus, "shortland-street-show",
            "https://www.tvnz.co.nz/shows/shortland-street", "Shortland Street",
            "Angular SSR document title is the series brand"),

        // Disney+ — often geo/auth walled
        Case(StreamingScraperProvider.DisneyPlus, "andor-series",
            "https://www.disneyplus.com/series/andor", "Andor",
            "Series path; live scrape may 404 outside a licensed region"),
        Case(StreamingScraperProvider.DisneyPlus, "encanto-film",
            "https://www.disneyplus.com/movies/encanto", null,
            "Movie path; ShowName must stay null; live scrape may 404 outside a licensed region"),

        // discovery+ — often geo/auth walled
        Case(StreamingScraperProvider.DiscoveryPlus, "gold-rush-show",
            "https://www.discoveryplus.com/show/gold-rush", "Gold Rush",
            "Show path; live scrape may 404 outside a licensed region"),
    ];

    private static StreamingScraperCanonicalCase Case(
        StreamingScraperProvider provider,
        string caseId,
        string url,
        string? expectedPodcastName,
        string stabilityNote) =>
        new(provider, caseId, new Uri(url), expectedPodcastName, stabilityNote);
}

public enum StreamingScraperProvider
{
    BbcSounds,
    BbcIplayer,
    Netflix,
    AmazonPrime,
    Vimeo,
    Itvx,
    Channel4,
    Fawesome,
    ParamountPlus,
    HboMax,
    PlaySuisse,
    TvnzPlus,
    DisneyPlus,
    DiscoveryPlus
}

public sealed record StreamingScraperCanonicalCase(
    StreamingScraperProvider Provider,
    string CaseId,
    Uri Url,
    string? ExpectedPodcastName,
    string StabilityNote)
{
    public override string ToString() => $"{Provider}/{CaseId}";
}
