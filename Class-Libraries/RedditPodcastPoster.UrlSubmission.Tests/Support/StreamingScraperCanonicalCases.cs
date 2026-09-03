namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

/// <summary>
/// Canonical live streaming URLs exercised by <see cref="BusinessRules.UrlSubmission.StreamingScraperUrlMembershipLookupRules"/>.
/// </summary>
/// <remarks>
/// <para>Live HTTP is the default path (detects provider HTML drift). Set <c>SKIP_LIVE_STREAMING_SCRAPER_TESTS=1</c> only in constrained environments.</para>
/// <para>
/// | Provider | Case | URL | Expected podcastName | Why stable / shape |
/// |----------|------|-----|-------------------|-------------------|
/// | BBC Sounds | desert-island-discs-brand | https://www.bbc.co.uk/sounds/play/m001ht9w | Desert Island Discs | Flagship Radio 4 brand; programme primary ≠ episode title |
/// | BBC Sounds | in-our-time-brand | https://www.bbc.co.uk/sounds/play/m001kqj2 | In Our Time | Long-running discussion series brand |
/// | BBC Sounds | news-quiz-brand | https://www.bbc.co.uk/sounds/play/m001w7fq | The News Quiz | Panel comedy brand distinct from episode headline |
/// | BBC Sounds | more-or-less-brand | https://www.bbc.co.uk/sounds/play/p0f76z6b | More or Less | Stats programme brand vs episode title |
/// | BBC Sounds | today-in-parliament-brand | https://www.bbc.co.uk/sounds/play/m00030mp | Today in Parliament | Parliamentary digest brand |
/// | BBC iPlayer | doctor-who-space-babies | https://www.bbc.co.uk/iplayer/episode/m001z8bz/doctor-who-season-1-1-space-babies | Doctor Who | og:video:series + redux subtitle; available &gt;1 year |
/// | BBC iPlayer | doctor-who-devils-chord | https://www.bbc.co.uk/iplayer/episode/m001z8c7/doctor-who-season-1-2-the-devils-chord | Doctor Who | Second-series episode; series metadata edge |
/// | BBC iPlayer | doctor-who-rose-archive | https://www.bbc.co.uk/iplayer/episode/b0074dlv/doctor-who-20052022-series-1-1-rose | Doctor Who (2005–2022) | Archive classic episode; long availability |
/// | BBC iPlayer | panorama-scams-ai | https://www.bbc.co.uk/iplayer/episode/m002wwqx/panorama-scams-lies-and-ai | Panorama | Current-affairs strand brand |
/// | BBC iPlayer | panorama-undercover-police | https://www.bbc.co.uk/iplayer/episode/m002k7k6/panorama-undercover-in-the-police | Panorama | Investigative strand brand |
/// | Netflix | stranger-things-watch | https://www.netflix.com/watch/80077368 | Stranger Things | Episode watch page; episode title ≠ series |
/// | Netflix | black-mirror-watch | https://www.netflix.com/watch/70264888 | Black Mirror | Anthology episode watch page |
/// | Netflix | the-crown-watch | https://www.netflix.com/watch/80068870 | The Crown | Period drama episode watch page |
/// | Netflix | glass-onion-film | https://www.netflix.com/title/81280792 | null | Standalone film catalogue page |
/// | Netflix | the-irishman-film | https://www.netflix.com/title/80175798 | null | Standalone film catalogue page |
/// | Amazon Prime | the-boys-season-1 | https://www.primevideo.com/detail/0S1FYJ3LY9KTL9C7WFFAGA9F6F | The Boys | Flagship series season detail page |
/// | Amazon Prime | clarksons-farm-season-5 | https://www.primevideo.com/detail/0OT6JCWNTHGSU7KAL5EBV7UJ5P | Clarkson's Farm | Reality farming series detail page |
/// | Amazon Prime | rings-of-power-season-1 | https://www.primevideo.com/detail/0TUVXIO58IUNEPNBF8363Z7YGL | The Lord of the Rings: The Rings of Power | Epic fantasy series detail page |
/// | Amazon Prime | air-film | https://www.primevideo.com/detail/0IKIGIXFRNQP5ZT74X0037B4X5 | null | Standalone film detail page |
/// | Amazon Prime | manchester-by-the-sea-film | https://www.primevideo.com/detail/0Q9AXD5XXKTFYLGE5ET1QXN4EJ | null | Standalone film detail page |
/// | Vimeo | ted-talk | https://vimeo.com/148751763 | TED | Institutional TED upload; oEmbed author |
/// | Vimeo | vimeo-showcase | https://vimeo.com/76979871 | Vimeo | Platform showcase upload |
/// | Vimeo | vimeo-staff-picks | https://vimeo.com/1084537 | Vimeo Staff | Staff picks channel upload |
/// | Vimeo | national-geographic | https://vimeo.com/25397435 | National Geographic | Publisher channel upload |
/// | Vimeo | new-yorker | https://vimeo.com/357274789 | The New Yorker | Magazine channel upload |
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
        Case(StreamingScraperProvider.BbcSounds, "desert-island-discs-brand",
            "https://www.bbc.co.uk/sounds/play/m001ht9w", "Desert Island Discs",
            "Flagship Radio 4 brand; programme primary differs from episode title"),
        Case(StreamingScraperProvider.BbcSounds, "in-our-time-brand",
            "https://www.bbc.co.uk/sounds/play/m001kqj2", "In Our Time",
            "Long-running discussion series brand"),
        Case(StreamingScraperProvider.BbcSounds, "news-quiz-brand",
            "https://www.bbc.co.uk/sounds/play/m001w7fq", "The News Quiz",
            "Panel comedy brand distinct from episode headline"),
        Case(StreamingScraperProvider.BbcSounds, "more-or-less-brand",
            "https://www.bbc.co.uk/sounds/play/p0f76z6b", "More or Less",
            "Stats programme brand vs episode title"),
        Case(StreamingScraperProvider.BbcSounds, "today-in-parliament-brand",
            "https://www.bbc.co.uk/sounds/play/m00030mp", "Today in Parliament",
            "Parliamentary digest brand"),

        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-space-babies",
            "https://www.bbc.co.uk/iplayer/episode/m001z8bz/doctor-who-season-1-1-space-babies", "Doctor Who",
            "og:video:series with redux subtitle; available over a year"),
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-devils-chord",
            "https://www.bbc.co.uk/iplayer/episode/m001z8c7/doctor-who-season-1-2-the-devils-chord", "Doctor Who",
            "Second-series episode; series metadata edge"),
        Case(StreamingScraperProvider.BbcIplayer, "doctor-who-rose-archive",
            "https://www.bbc.co.uk/iplayer/episode/b0074dlv/doctor-who-20052022-series-1-1-rose", "Doctor Who (2005–2022)",
            "Archive classic episode; long availability"),
        Case(StreamingScraperProvider.BbcIplayer, "panorama-scams-ai",
            "https://www.bbc.co.uk/iplayer/episode/m002wwqx/panorama-scams-lies-and-ai", "Panorama",
            "Current-affairs strand brand"),
        Case(StreamingScraperProvider.BbcIplayer, "panorama-undercover-police",
            "https://www.bbc.co.uk/iplayer/episode/m002k7k6/panorama-undercover-in-the-police", "Panorama",
            "Investigative strand brand"),

        Case(StreamingScraperProvider.Netflix, "stranger-things-watch",
            "https://www.netflix.com/watch/80077368", "Stranger Things",
            "Episode watch page; episode title differs from series"),
        Case(StreamingScraperProvider.Netflix, "black-mirror-watch",
            "https://www.netflix.com/watch/70264888", "Black Mirror",
            "Anthology episode watch page"),
        Case(StreamingScraperProvider.Netflix, "the-crown-watch",
            "https://www.netflix.com/watch/80068870", "The Crown",
            "Period drama episode watch page"),
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

        Case(StreamingScraperProvider.Vimeo, "ted-talk",
            "https://vimeo.com/148751763", "TED",
            "Institutional TED upload; oEmbed author"),
        Case(StreamingScraperProvider.Vimeo, "vimeo-showcase",
            "https://vimeo.com/76979871", "Vimeo",
            "Platform showcase upload"),
        Case(StreamingScraperProvider.Vimeo, "vimeo-staff-picks",
            "https://vimeo.com/1084537", "Vimeo Staff",
            "Staff picks channel upload"),
        Case(StreamingScraperProvider.Vimeo, "national-geographic",
            "https://vimeo.com/25397435", "National Geographic",
            "Publisher channel upload"),
        Case(StreamingScraperProvider.Vimeo, "new-yorker",
            "https://vimeo.com/357274789", "The New Yorker",
            "Magazine channel upload"),
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
