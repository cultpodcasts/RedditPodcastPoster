namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

/// <summary>
/// Canonical live streaming URLs exercised by <see cref="BusinessRules.UrlSubmission.StreamingScraperUrlMembershipLookupRules"/>.
/// </summary>
/// <remarks>
/// <para>Live HTTP is the default path (detects provider HTML drift). Set <c>SKIP_LIVE_STREAMING_SCRAPER_TESTS=1</c> only in constrained environments.</para>
/// <para>
/// | Provider | Case | URL | Expected podcastName | Why stable / shape |
/// |----------|------|-----|---------------------|-------------------|
/// | BBC Sounds | desert-island-discs-jony-ive | https://www.bbc.co.uk/sounds/play/m00289vf | Desert Island Discs | Flagship Radio 4 brand; primary brand ≠ guest episode title |
/// | BBC Sounds | desert-island-discs-nick-cave | https://www.bbc.co.uk/sounds/play/m0027cgl | Desert Island Discs | Second Desert Island Discs specimen |
/// | BBC Sounds | in-our-time-battle-valmy | https://www.bbc.co.uk/sounds/play/m0026vs5 | In Our Time | Long-running discussion series brand |
/// | BBC Sounds | in-our-time-cyrus | https://www.bbc.co.uk/sounds/play/m0028tzc | In Our Time | Second In Our Time specimen |
/// | BBC Sounds | in-our-time-pollination | https://www.bbc.co.uk/sounds/play/m0028jtx | In Our Time | Episode headline differs from programme brand |
/// | BBC iPlayer | doctor-who-space-babies | https://www.bbc.co.uk/iplayer/episode/m001z8bz/doctor-who-season-1-1-space-babies | Doctor Who | og:video:series + redux subtitle edge; available &gt;1 year |
/// | BBC iPlayer | doctor-who-devils-chord | https://www.bbc.co.uk/iplayer/episode/m001z8c7/doctor-who-season-1-2-the-devils-chord | Doctor Who | Second Doctor Who episode in same run |
/// | BBC iPlayer | doctor-who-rose-archive | https://www.bbc.co.uk/iplayer/episode/b0074dlv/doctor-who-20052022-series-1-1-rose | Doctor Who (2005–2022) | Archive classic with distinct franchise title |
/// | BBC iPlayer | panorama-scams-ai | https://www.bbc.co.uk/iplayer/episode/m002wwqx/panorama-scams-lies-and-ai | Panorama | Current-affairs strand brand |
/// | BBC iPlayer | panorama-undercover-police | https://www.bbc.co.uk/iplayer/episode/m002k7k6/panorama-undercover-in-the-police | Panorama | Investigative strand brand |
/// | Netflix | stranger-things-catalogue | https://www.netflix.com/title/80057281 | null | Series catalogue page: og:title is the show, no distinct series field |
/// | Netflix | black-mirror-catalogue | https://www.netflix.com/title/70264888 | null | Anthology catalogue page without episode-level series split |
/// | Netflix | the-crown-catalogue | https://www.netflix.com/title/80025678 | null | Period drama catalogue page |
/// | Netflix | glass-onion-film | https://www.netflix.com/title/81280792 | null | Standalone film catalogue page |
/// | Netflix | the-irishman-film | https://www.netflix.com/title/80175798 | null | Standalone film catalogue page |
/// | Amazon Prime | the-boys-season-1 | https://www.primevideo.com/detail/0S1FYJ3LY9KTL9C7WFFAGA9F6F | The Boys | Flagship series season detail page |
/// | Amazon Prime | clarksons-farm-season-5 | https://www.primevideo.com/detail/0OT6JCWNTHGSU7KAL5EBV7UJ5P | Clarkson's Farm | Reality farming series detail page |
/// | Amazon Prime | rings-of-power-season-1 | https://www.primevideo.com/detail/0TUVXIO58IUNEPNBF8363Z7YGL | The Lord of the Rings: The Rings of Power | Epic fantasy series detail page |
/// | Amazon Prime | air-film | https://www.primevideo.com/detail/0IKIGIXFRNQP5ZT74X0037B4X5 | null | Standalone film detail page |
/// | Amazon Prime | manchester-by-the-sea-film | https://www.primevideo.com/detail/0Q9AXD5XXKTFYLGE5ET1QXN4EJ | null | Standalone film detail page |
/// | Vimeo | big-buck-bunny | https://vimeo.com/1084537 | Blender | Open channel upload via oEmbed author |
/// | Vimeo | the-city-limits | https://vimeo.com/23237102 | Dominic | Independent creator upload |
/// | Vimeo | the-mountain | https://vimeo.com/22439234 | TSO Photography | Time-lapse creator channel |
/// | Vimeo | travis-scott-clip | https://vimeo.com/357274789 | Wesley Luyten | User upload with personal channel name |
/// | Vimeo | cibc-corporate | https://vimeo.com/104653183 | CIBC | Corporate channel upload |
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

    public static IEnumerable<StreamingScraperCanonicalCase> All =>
    [
        Case(StreamingScraperProvider.BbcSounds, "desert-island-discs-jony-ive",
            "https://www.bbc.co.uk/sounds/play/m00289vf", "Desert Island Discs",
            "Flagship Radio 4 brand; primary brand differs from guest episode title"),
        Case(StreamingScraperProvider.BbcSounds, "desert-island-discs-nick-cave",
            "https://www.bbc.co.uk/sounds/play/m0027cgl", "Desert Island Discs",
            "Second Desert Island Discs specimen"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-battle-valmy",
            "https://www.bbc.co.uk/sounds/play/m0026vs5", "In Our Time",
            "Long-running discussion series brand"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-cyrus",
            "https://www.bbc.co.uk/sounds/play/m0028tzc", "In Our Time",
            "Second In Our Time specimen"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-pollination",
            "https://www.bbc.co.uk/sounds/play/m0028jtx", "In Our Time",
            "Episode headline differs from programme brand"),

        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-space-babies",
            "https://www.bbc.co.uk/iplayer/episode/m001z8bz/doctor-who-season-1-1-space-babies", "Doctor Who",
            "og:video:series with redux subtitle; available over a year"),
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-devils-chord",
            "https://www.bbc.co.uk/iplayer/episode/m001z8c7/doctor-who-season-1-2-the-devils-chord", "Doctor Who",
            "Second Doctor Who episode in same run"),
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-rose-archive",
            "https://www.bbc.co.uk/iplayer/episode/b0074dlv/doctor-who-20052022-series-1-1-rose", "Doctor Who (2005–2022)",
            "Archive classic with distinct franchise title"),
        Case(StreamingScraperProvider.BbcIplayer, "panorama-scams-ai",
            "https://www.bbc.co.uk/iplayer/episode/m002wwqx/panorama-scams-lies-and-ai", "Panorama",
            "Current-affairs strand brand"),
        Case(StreamingScraperProvider.BbcIplayer, "panorama-undercover-police",
            "https://www.bbc.co.uk/iplayer/episode/m002k7k6/panorama-undercover-in-the-police", "Panorama",
            "Investigative strand brand"),

        Case(StreamingScraperProvider.Netflix, "stranger-things-catalogue",
            "https://www.netflix.com/title/80057281", null,
            "Series catalogue page: og:title is the show, no distinct series field"),
        Case(StreamingScraperProvider.Netflix, "black-mirror-catalogue",
            "https://www.netflix.com/title/70264888", null,
            "Anthology catalogue page without episode-level series split"),
        Case(StreamingScraperProvider.Netflix, "the-crown-catalogue",
            "https://www.netflix.com/title/80025678", null,
            "Period drama catalogue page"),
        Case(StreamingScraperProvider.Netflix, "glass-onion-film",
            "https://www.netflix.com/title/81280792", null,
            "Standalone film catalogue page"),
        Case(StreamingScraperProvider.Netflix, "the-irishman-film",
            "https://www.netflix.com/title/80175798", null,
            "Standalone film catalogue page"),

        Case(StreamingScraperProvider.AmazonPrime, "the-boys-season-1",
            "https://www.primevideo.com/detail/0S1FYJ3LY9KTL9C7WFFAGA9F6F", "The Boys",
            "Flagship series season detail page"),
        Case(StreamingScraperProvider.AmazonPrime, "clarksons-farm-season-5",
            "https://www.primevideo.com/detail/0OT6JCWNTHGSU7KAL5EBV7UJ5P", "Clarkson's Farm",
            "Reality farming series detail page"),
        Case(StreamingScraperProvider.AmazonPrime, "rings-of-power-season-1",
            "https://www.primevideo.com/detail/0TUVXIO58IUNEPNBF8363Z7YGL",
            "The Lord of the Rings: The Rings of Power",
            "Epic fantasy series detail page"),
        Case(StreamingScraperProvider.AmazonPrime, "air-film",
            "https://www.primevideo.com/detail/0IKIGIXFRNQP5ZT74X0037B4X5", null,
            "Standalone film detail page"),
        Case(StreamingScraperProvider.AmazonPrime, "manchester-by-the-sea-film",
            "https://www.primevideo.com/detail/0Q9AXD5XXKTFYLGE5ET1QXN4EJ", null,
            "Standalone film detail page"),

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
    Vimeo
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
