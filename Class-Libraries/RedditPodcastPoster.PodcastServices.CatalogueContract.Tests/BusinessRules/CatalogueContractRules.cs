using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.CatalogueContract.Tests.BusinessRules;

/// <summary>
/// Catalogue contract: the same lookup against the same show catalogue gives the same result through the REAL
/// <see cref="SpotifyEpisodeResolver"/> and <see cref="AppleEpisodeResolver"/>; only the platform HTTP clients are faked.
/// A platform-specific fork in candidate sourcing, subject classification or matching strategy fails here.
/// </summary>
public class CatalogueContractRules
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private const string Title = "Lantern Harbour Night Stories With A Guest From The Archive";

    private static readonly CatalogueCandidatePolicyOptions Options = new() { RecentShowEpisodesPageSize = 2 };

    public enum Platform { Spotify, Apple }

    public sealed record Item(string Id, string Title, DateTime Release, TimeSpan Length);

    public sealed record Case(
        string Name,
        string LookupTitle,
        DateTime Released,
        TimeSpan Length,
        Service? Authority,
        bool Enriching,
        bool ShowKnown,
        string? StaleEpisodeId,
        Item[] ShowNewestFirst,
        Item[] TitleSearchHits,
        string? ExpectedId)
    {
        public override string ToString() => Name;
    }

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

    private static readonly Dictionary<string, Case> AllCases = new Case[]
    {
        new("real-divergent-pair",
            "Why Is Everyone Lying to You for Money? The Great Crypto Scam EXPOSED (w/ Ben McKenzie)",
            new DateTime(2026, 10, 12), new TimeSpan(0, 56, 49), Service.YouTube, true, true, null,
            [
                new("1001", "Everyone Is Lying to You for Money, Crypto and Cult Psychology with Ben McKenzie: My conversation with the author of Easy Money about financial manipulation, the need to belong, and protecting our freedom of mind.",
                    new DateTime(2026, 10, 12), new TimeSpan(0, 59, 29)),
                new("1002", "Volcanic Origami Workshop", new DateTime(2026, 10, 5), new TimeSpan(0, 58, 10))
            ],
            [], "1001"),
        new("older-episode-via-search", Title, Now.Date.AddDays(-120), TimeSpan.FromMinutes(44), Service.Spotify, false, true, null,
            [
                new("2001", "Volcanic Origami Workshop", Now.Date.AddDays(-1), TimeSpan.FromMinutes(44)),
                new("2002", "Quiet Orchard Interview", Now.Date.AddDays(-8), TimeSpan.FromMinutes(44)),
                new("2003", Title, Now.Date.AddDays(-120), TimeSpan.FromMinutes(44))
            ],
            [new("2003", Title, Now.Date.AddDays(-120), TimeSpan.FromMinutes(44))], "2003"),
        new("no-show-id", Title, Now.Date.AddDays(-1), TimeSpan.FromMinutes(44), Service.Spotify, false, false, null,
            [new("3001", Title, Now.Date.AddDays(-1), TimeSpan.FromMinutes(44))], [], null),
        new("recent-miss-falls-back-to-search", Title, Now.Date.AddDays(-3), TimeSpan.FromMinutes(44), Service.Spotify, false, true, null,
            [
                new("4001", "Volcanic Origami Workshop", Now.Date.AddDays(-1), TimeSpan.FromMinutes(44)),
                new("4002", "Quiet Orchard Interview", Now.Date.AddDays(-2), TimeSpan.FromMinutes(44)),
                new("4003", Title, Now.Date.AddDays(-3), TimeSpan.FromMinutes(44))
            ],
            [new("4003", Title, Now.Date.AddDays(-3), TimeSpan.FromMinutes(44))], "4003"),
        new("known-id-miss", Title, Now.Date.AddDays(-2), TimeSpan.FromMinutes(44), Service.YouTube, false, true, "5999",
            [new("5001", Title, Now.Date.AddDays(-2), TimeSpan.FromMinutes(44))], [], "5001"),
        new("non-youtube-out-of-band", Title, Now.Date.AddDays(-60), TimeSpan.FromMinutes(44), Service.Spotify, false, true, null,
            [
                new("6001", "Volcanic Origami Workshop", Now.Date.AddDays(-60), TimeSpan.FromMinutes(61)),
                new("6002", Title, Now.Date.AddDays(-60).AddYears(-3), TimeSpan.FromMinutes(61))
            ],
            [
                new("6001", "Volcanic Origami Workshop", Now.Date.AddDays(-60), TimeSpan.FromMinutes(61)),
                new("6002", Title, Now.Date.AddDays(-60).AddYears(-3), TimeSpan.FromMinutes(61))
            ], "6002")
    }.ToDictionary(c => c.Name);

    [Theory(DisplayName =
        "Spotify and Apple resolve the same lookup against the same catalogue to the same episode (real divergent pair, " +
        "older episode via search, no show id, recent miss falling back to search, known-id miss, non-YouTube out-of-band) " +
        "because candidate sourcing, subjects and strategy are one shared policy.")]
    [MemberData(nameof(Cases))]
    public async Task Same_lookup_same_result_on_every_platform(Platform platform, string caseName)
    {
        // Arrange
        var c = AllCases[caseName];
        var policy = CatalogueTestServices.CreatePolicy(
            new TitleSubstringSubjectClassifier(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ben McKenzie"] = ["Cult Psychology", "Multi-Level Marketing"]
            }),
            new FixedTimeProvider(Now),
            Options);

        // Act
        var result = platform == Platform.Spotify ? await RunSpotify(c, policy) : await RunApple(c, policy);

        // Assert
        result.Should().Be(c.ExpectedId, c.Name);
    }

    private static async Task<string?> RunSpotify(Case c, ICatalogueCandidatePolicy policy)
    {
        var client = new Mock<ISpotifyClientWrapper>();
        client.Setup(x => x.GetShowEpisodes(It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = c.ShowNewestFirst.Select(ToSpotify).ToList() });
        foreach (var item in c.ShowNewestFirst)
        {
            client.Setup(x => x.GetFullEpisode(item.Id, It.IsAny<EpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FullEpisode { Id = item.Id, Name = item.Title, IsPlayable = true });
        }
        var titleSearch = new Mock<ISpotifyEpisodeTitleSearch>();
        titleSearch.Setup(x => x.FindCandidates(It.IsAny<FindSpotifyEpisodeRequest>(), It.IsAny<IndexingContext>(), It.IsAny<string>()))
            .ReturnsAsync(c.TitleSearchHits.Select(ToSpotify).ToList());
        var sut = new SpotifyEpisodeResolver(
            titleSearch.Object,
            client.Object,
            new SpotifyRecentShowEpisodesCache(client.Object),
            policy,
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            NullLogger<SpotifyEpisodeResolver>.Instance);

        var result = await sut.FindEpisode(
            new FindSpotifyEpisodeRequest(
                PodcastSpotifyId: c.ShowKnown ? "show" : string.Empty,
                PodcastName: "Podcast",
                EpisodeSpotifyId: c.StaleEpisodeId ?? string.Empty,
                EpisodeTitle: c.LookupTitle,
                Released: c.Released,
                HasExpensiveSpotifyEpisodesQuery: false,
                ReleaseAuthority: c.Authority,
                Length: c.Length,
                EnrichingYouTubeDiscoveredEpisode: c.Enriching),
            new IndexingContext());
        return result.FullEpisode?.Id;
    }

    private static async Task<string?> RunApple(Case c, ICatalogueCandidatePolicy policy)
    {
        var service = new Mock<ICachedApplePodcastService>();
        service.Setup(x => x.GetEpisodes(It.IsAny<ApplePodcastId>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(c.ShowNewestFirst.Select(ToApple).ToList());
        var sut = new AppleEpisodeResolver(service.Object, policy, NullLogger<AppleEpisodeResolver>.Instance);

        var result = await sut.FindEpisode(
            new FindAppleEpisodeRequest(
                PodcastAppleId: c.ShowKnown ? 42 : null,
                PodcastName: "Podcast",
                EpisodeAppleId: c.StaleEpisodeId == null ? null : long.Parse(c.StaleEpisodeId),
                EpisodeTitle: c.LookupTitle,
                Released: c.Released,
                ReleaseAuthority: c.Authority,
                EpisodeLength: c.Length,
                YouTubePublishingDelay: null,
                EnrichingYouTubeDiscoveredEpisode: c.Enriching),
            new IndexingContext());
        return result?.Id.ToString();
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
            new Uri($"https://podcasts.apple.com/podcast/id42?i={item.Id}"), string.Empty, false);
}
