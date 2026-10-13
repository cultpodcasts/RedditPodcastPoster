using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Tests.Support;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.CatalogueContract.Tests.BusinessRules;

/// <summary>
/// Catalogue contract: the same lookup (flow, show size, release date) against the same show catalogue gives the same
/// result AND the same request profile (show pages read, title searches made) through the REAL
/// <see cref="SpotifyEpisodeResolver"/> and <see cref="Apple.Resolvers.AppleEpisodeResolver"/>, the real
/// <see cref="EpisodeLookupPlanner"/> and <see cref="CatalogueLookupExecutor"/>; only the platform HTTP clients are faked.
/// Both fakes serve pages of 50 newest-first, so a platform-specific fork in strategy selection fails here.
/// </summary>
public class CatalogueContractRules
{
    private static readonly DateTime Now = DomainTestFixture.UtcDateDaysAgo(0).AddHours(12);
    private const string Title = "Lantern Harbour Night Stories With A Guest From The Archive";
    private const int PageSize = SpotifyShowEpisodePages.PageSize;

    // Ben McKenzie / Steve Hassan (podcast 186529ba-43e3-497f-9770-9a9d46232333, episode 67b9dfa6-bd09-46d1-9859-decbd7d1a90b):
    // Spotify show 3fy5dEPagR2T8MPeS7ieAM, Spotify episode 3RtQ7heOajiCNdikDXUTlW, Apple podcast 1603773245 (trackCount 255).
    private const string BenYouTubeTitle =
        "Why Is Everyone Lying to You for Money? The Great Crypto Scam EXPOSED (w/ Ben McKenzie)";
    private const string BenAudioTitle =
        "Everyone Is Lying to You for Money, Crypto and Cult Psychology with Ben McKenzie: My conversation with the author of Easy Money about financial manipulation, the need to belong, and protecting our freedom of mind.";
    private const int BenShowEpisodes = 255;

    public enum Platform { Spotify, Apple }

    public sealed record Item(string Id, string Title, DateTime Release, TimeSpan Length);

    public sealed record Case(
        string Name,
        EpisodeLookupFlow Flow,
        string LookupTitle,
        DateTime Released,
        TimeSpan Length,
        Service? Authority,
        bool Enriching,
        bool ShowKnown,
        int? StoredShowEpisodeCount,
        string? StaleEpisodeId,
        Item[] ShowNewestFirst,
        Item[] TitleSearchHits,
        string? ExpectedId,
        int ExpectedPagesRead,
        int ExpectedSearches)
    {
        public override string ToString() => Name;
    }

    public sealed record Outcome(string? Id, int PagesRead, int Searches);

    public static TheoryData<Platform, string> Cases()
    {
        var data = new TheoryData<Platform, string>();
        foreach (var c in AllCases.Keys)
        {
            data.Add(Platform.Spotify, c);
            data.Add(Platform.Apple, c);
        }

        return data;
    }

    private static Item[] Filler(int count, int idBase, int startDaysAgo = 0) =>
        Enumerable.Range(0, count)
            .Select(i => new Item((idBase + i).ToString(), $"Unrelated Episode Number {i} About Volcanic Origami",
                Now.Date.AddDays(-(startDaysAgo + i)), TimeSpan.FromMinutes(20)))
            .ToArray();

    /// <summary>A daily show of <paramref name="count"/> episodes with <paramref name="target"/> at its own date.</summary>
    private static Item[] ShowWith(int count, int idBase, Item target) =>
        Filler(count - 1, idBase).Append(target).OrderByDescending(x => x.Release).ToArray();

    private static readonly Item BenAudio = new("1001", BenAudioTitle, Now.Date.AddDays(-1), new TimeSpan(0, 59, 29));

    private static Item Target(string id, int daysAgo) =>
        new(id, Title, Now.Date.AddDays(-daysAgo).AddHours(6), TimeSpan.FromMinutes(44));

    private static readonly Dictionary<string, Case> AllCases = new Case[]
    {
        // Index flow: newest pages until past the window, any size, no search. Unknown size (historic null) is probed.
        new("ben-mckenzie-index-flow", EpisodeLookupFlow.Index, BenYouTubeTitle, Now.Date.AddDays(-1), new TimeSpan(0, 56, 49),
            Service.YouTube, true, true, null, null, ShowWith(BenShowEpisodes, 10000, BenAudio), [], "1001", 1, 0),
        // Submit of a recent episode on a large/unknown show: newest page first, before searching.
        new("ben-mckenzie-submit-flow", EpisodeLookupFlow.Submit, BenYouTubeTitle, Now.Date.AddDays(-1), new TimeSpan(0, 56, 49),
            Service.YouTube, true, true, null, null, ShowWith(BenShowEpisodes, 10000, BenAudio), [], "1001", 1, 0),
        // Old episode on a large show: search first, no pages read.
        new("old-episode-large-show-via-search", EpisodeLookupFlow.Submit, Title, Target("2003", 300).Release, TimeSpan.FromMinutes(44),
            Service.Spotify, false, true, 600, null, ShowWith(600, 20000, Target("2003", 300)), [Target("2003", 300)], "2003", 0, 1),
        new("old-episode-discovery-flow-via-search", EpisodeLookupFlow.Discovery, Title, Target("2103", 300).Release, TimeSpan.FromMinutes(44),
            Service.Spotify, false, true, 600, null, ShowWith(600, 21000, Target("2103", 300)), [Target("2103", 300)], "2103", 0, 1),
        // Search misses: the bounded walk (Spotify 4 / Apple 5 pages) stops at the release band, so both read 3 pages here.
        new("old-episode-search-miss-bounded-walk", EpisodeLookupFlow.Enrich, Title, Target("2203", 120).Release, TimeSpan.FromMinutes(44),
            Service.Spotify, false, true, 600, null, ShowWith(600, 22000, Target("2203", 120)), [], "2203", 3, 1),
        // Small show: full read in any flow, no search.
        new("small-show-full-read", EpisodeLookupFlow.Submit, Title, Target("3003", 119).Release, TimeSpan.FromMinutes(44),
            Service.Spotify, false, true, 120, null, ShowWith(120, 30000, Target("3003", 119)), [], "3003", 3, 0),
        new("no-show-id", EpisodeLookupFlow.Submit, Title, Now.Date.AddDays(-1), TimeSpan.FromMinutes(44), Service.Spotify, false, false, null, null,
            [new("4001", Title, Now.Date.AddDays(-1), TimeSpan.FromMinutes(44))], [], null, 0, -1),
        new("stale-id-falls-through", EpisodeLookupFlow.Submit, Title, Now.Date.AddDays(-2), TimeSpan.FromMinutes(44), Service.YouTube, false, true,
            null, "5999", [new("5001", Title, Now.Date.AddDays(-2), TimeSpan.FromMinutes(44))], [], "5001", 1, 0),
        new("non-youtube-out-of-band", EpisodeLookupFlow.Submit, Title, Now.Date.AddDays(-60), TimeSpan.FromMinutes(44), Service.Spotify, false, true,
            600, null, Filler(600, 60000),
            [
                new("6001", "Volcanic Origami Workshop", Now.Date.AddDays(-60), TimeSpan.FromMinutes(61)),
                new("6002", Title, Now.Date.AddDays(-60).AddYears(-3), TimeSpan.FromMinutes(61))
            ], "6002", 0, 1)
    }.ToDictionary(c => c.Name);

    [Theory(DisplayName =
        "Spotify and Apple resolve the same lookup (flow x show size x release age) to the same episode with the same pages read and " +
        "searches made (Ben McKenzie in Index and Submit flows, old episode via search, bounded walk, small-show full read, no show id, " +
        "stale id, out-of-band) because strategy selection is one shared planner.")]
    [MemberData(nameof(Cases))]
    public async Task Same_lookup_same_result_on_every_platform(Platform platform, string caseName)
    {
        // Arrange
        var c = AllCases[caseName];

        // Act
        var outcome = platform == Platform.Spotify ? await RunSpotify(c) : await RunApple(c);

        // Assert
        outcome.Id.Should().Be(c.ExpectedId, c.Name);
        outcome.PagesRead.Should().Be(c.ExpectedPagesRead, c.Name);
        if (c.ExpectedSearches >= 0)
        {
            outcome.Searches.Should().Be(c.ExpectedSearches, c.Name);
        }
    }

    [Theory(DisplayName =
        "When an old episode of a large show misses the title search and is older than the bounded walk reaches, both platforms stop at " +
        "their page cap (Spotify 4, Apple 5) and miss cleanly, because the walk is the last resort and must stay bounded.")]
    [InlineData(Platform.Spotify)]
    [InlineData(Platform.Apple)]
    public async Task Bounded_walk_stops_at_platform_cap(Platform platform)
    {
        // Arrange
        var options = new EpisodeLookupOptions();
        var cap = platform == Platform.Spotify ? options.SpotifyBoundedWalkPages : options.AppleBoundedWalkPages;
        var c = new Case("beyond-cap", EpisodeLookupFlow.Submit, Title, Target("7003", 500).Release, TimeSpan.FromMinutes(44),
            Service.Spotify, false, true, 600, null, ShowWith(600, 70000, Target("7003", 500)), [], null, cap, 1);

        // Act
        var outcome = platform == Platform.Spotify ? await RunSpotify(c) : await RunApple(c);

        // Assert
        outcome.Id.Should().BeNull();
        outcome.PagesRead.Should().Be(cap);
        outcome.Searches.Should().Be(1);
    }

    [Fact(DisplayName =
        "The caller's CancellationToken reaches every platform call through the timeout guard on both resolvers " +
        "(Spotify show page and title search, Apple page and iTunes lookup), so a cancelled lookup stops instead of finishing its HTTP calls.")]
    public async Task Cancellation_token_is_threaded_to_platform_calls()
    {
        // Arrange
        var c = AllCases["ben-mckenzie-submit-flow"];
        using var spotifyCts = new CancellationTokenSource();
        using var appleCts = new CancellationTokenSource();
        var observed = new List<bool>();

        void Observe(CancellationTokenSource source, CancellationToken token)
        {
            source.Cancel();
            observed.Add(token.IsCancellationRequested);
        }

        // Act
        await RunSpotify(c, ct => Observe(spotifyCts, ct), spotifyCts.Token);
        await RunApple(c, ct => Observe(appleCts, ct), appleCts.Token);

        // Assert
        observed.Should().NotBeEmpty().And.OnlyContain(x => x);
    }

    private static TitleSubstringSubjectClassifier Subjects() =>
        new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ben McKenzie"] = ["Cult Psychology", "Multi-Level Marketing"]
        });

    private static CatalogueLookupExecutor Executor() =>
        CatalogueTestServices.CreateExecutor(Subjects(), new FixedTimeProvider(Now));

    private static IndexingContext Context(Case c) =>
        new(Now.AddDays(-7)) { LookupFlow = c.Flow };

    private static async Task<Outcome> RunSpotify(
        Case c, Action<CancellationToken>? onRequest = null, CancellationToken cancellationToken = default)
    {
        var catalogue = c.ShowNewestFirst.Select(ToSpotify).ToList();
        var mocker = new AutoMocker();
        var client = mocker.GetMock<ISpotifyClientWrapper>();
        client.Setup(x => x.GetShowEpisodes(It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, ShowEpisodesRequest r, IndexingContext _, CancellationToken ct) =>
            {
                onRequest?.Invoke(ct);
                return new Paging<SimpleEpisode>
                {
                    Items = catalogue.Skip(r.Offset ?? 0).Take(r.Limit ?? 20).ToList(),
                    Total = catalogue.Count
                };
            });
        var known = c.ShowNewestFirst.Concat(c.TitleSearchHits).Select(x => x.Id).ToHashSet();
        client.Setup(x => x.GetFullEpisode(It.IsAny<string>(), It.IsAny<EpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, EpisodeRequest _, IndexingContext _, CancellationToken _) =>
                known.Contains(id) ? new FullEpisode { Id = id, Name = id, IsPlayable = true } : null);
        var titleSearch = mocker.GetMock<ISpotifyEpisodeTitleSearch>();
        titleSearch.Setup(x => x.FindCandidates(It.IsAny<FindSpotifyEpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FindSpotifyEpisodeRequest _, IndexingContext _, string _, CancellationToken ct) =>
            {
                onRequest?.Invoke(ct);
                return c.TitleSearchHits.Select(ToSpotify).ToList();
            });
        var guard = CatalogueTestServices.CreateGuard();
        var sut = new SpotifyEpisodeResolver(
            titleSearch.Object,
            client.Object,
            new SpotifyShowEpisodePages(client.Object, guard),
            Executor(),
            guard,
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            NullLogger<SpotifyEpisodeResolver>.Instance);

        var result = await sut.FindEpisode(
            new FindSpotifyEpisodeRequest(
                PodcastSpotifyId: c.ShowKnown ? "3fy5dEPagR2T8MPeS7ieAM" : string.Empty,
                PodcastName: "Podcast",
                EpisodeSpotifyId: c.StaleEpisodeId ?? string.Empty,
                EpisodeTitle: c.LookupTitle,
                Released: c.Released,
                HasExpensiveSpotifyEpisodesQuery: false,
                ReleaseAuthority: c.Authority,
                Length: c.Length,
                EnrichingYouTubeDiscoveredEpisode: c.Enriching,
                ShowEpisodeCount: c.StoredShowEpisodeCount),
            Context(c),
            cancellationToken: cancellationToken);
        var pages = client.Invocations.Count(x => x.Method.Name == nameof(ISpotifyClientWrapper.GetShowEpisodes));
        var searches = titleSearch.Invocations.Count;
        return new Outcome(result.FullEpisode?.Id, pages, searches);
    }

    private static async Task<Outcome> RunApple(
        Case c, Action<CancellationToken>? onRequest = null, CancellationToken cancellationToken = default)
    {
        var harness = AppleResolverHarness.Create(
            c.ShowNewestFirst.Select(ToApple),
            Subjects(),
            timeProvider: new FixedTimeProvider(Now),
            iTunesEpisodeCount: c.ShowNewestFirst.Length,
            searchHits: c.TitleSearchHits.Select(ToApple).ToList(),
            pageSize: PageSize,
            onRequest: onRequest);

        var result = await harness.Resolver.FindEpisode(
            new FindAppleEpisodeRequest(
                PodcastAppleId: c.ShowKnown ? 1603773245 : null,
                PodcastName: "Podcast",
                EpisodeAppleId: c.StaleEpisodeId == null ? null : long.Parse(c.StaleEpisodeId),
                EpisodeTitle: c.LookupTitle,
                Released: c.Released,
                ReleaseAuthority: c.Authority,
                EpisodeLength: c.Length,
                YouTubePublishingDelay: null,
                EnrichingYouTubeDiscoveredEpisode: c.Enriching,
                ShowEpisodeCount: c.StoredShowEpisodeCount),
            Context(c),
            cancellationToken: cancellationToken);
        return new Outcome(result?.Id.ToString(), harness.Service.PageCalls, harness.Lookup.SearchCalls);
    }

    private static SimpleEpisode ToSpotify(Item item) => new()
    {
        Id = item.Id,
        Name = item.Title,
        HtmlDescription = string.Empty,
        DurationMs = (int)item.Length.TotalMilliseconds,
        ReleaseDate = item.Release.ToString("yyyy-MM-dd"),
        Type = ItemType.Episode,
        IsPlayable = true
    };

    private static AppleEpisode ToApple(Item item) =>
        new(long.Parse(item.Id), item.Title, DateTime.SpecifyKind(item.Release, DateTimeKind.Utc), item.Length,
            new Uri($"https://podcasts.apple.com/podcast/id1603773245?i={item.Id}"), string.Empty, false);
}
