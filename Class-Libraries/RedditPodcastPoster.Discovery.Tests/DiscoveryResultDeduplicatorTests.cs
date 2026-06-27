using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery.Tests;

public class DiscoveryResultDeduplicatorTests
{
    private const int FuzzSeedCount = 100;
    private const string CosmosDocumentId = "d6d38104-5ba5-4dec-ada4-094c48f61808";
    private const int CosmosRawRowCount = 95;
    private const string KanunguEpisodeName = "The Kanungu Cult: When the World Was Supposed to End";

    private static readonly Uri NbaAppleUrl = new(
        "https://podcasts.apple.com/podcast/crossover-episode-hornets-and-timberwolves-trade/id995386468?i=1000774379000");

    private static readonly Guid LittleLiterId = Guid.Parse("2cfd4d11-0a00-48e9-b137-1991a63a7dbe");
    private static readonly Guid ConservativeCultId = Guid.Parse("b851a85e-9bb5-45e8-9934-3bcc692e54f6");
    private static readonly Guid ObamaSpeechId = Guid.Parse("490f9a6a-1531-4a63-bf1d-bda35c527f1f");
    private static readonly Guid MonsterClubId = Guid.Parse("8f8a8145-71aa-4bde-9300-eca7306e249f");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DiscoveryResultDeduplicator _sut = new();

    [Fact]
    public void Deduplicate_TransitiveChainRegression_KanunguDoesNotKeepNbaAppleUrl()
    {
        var kanungu = FindKanungu(_sut.Deduplicate(LoadTransitiveChainFixture()));

        kanungu.Urls.Apple.Should().BeNull();
        kanungu.Urls.Apple?.ToString().Should().NotContain("hornets-and-timberwolves");
    }

    [Fact]
    public void Deduplicate_TransitiveChainRegression_KanunguDoesNotInflateAcceptProbability()
    {
        var kanungu = FindKanungu(_sut.Deduplicate(LoadTransitiveChainFixture()));

        kanungu.AcceptProbability.Should().BeLessThan(0.1f);
        kanungu.AcceptProbability.Should().NotBe(0.9873123f);
    }

    [Fact]
    public void Deduplicate_TransitiveChainRegression_KanunguKeepsWinnerSubjectsOnly()
    {
        var kanungu = FindKanungu(_sut.Deduplicate(LoadTransitiveChainFixture()));

        kanungu.Subjects.Should().BeEquivalentTo(["Movement for the Restoration of the Ten Commandments of God"]);
        kanungu.Subjects.Should().NotContain("Peoples Temple");
        kanungu.Subjects.Should().NotContain("Theranos");
        kanungu.Subjects.Should().NotContain("Scientology");
    }

    [Fact]
    public void Deduplicate_TransitiveChainRegression_KanunguCombinesSameEpisodeUrls()
    {
        var kanungu = FindKanungu(_sut.Deduplicate(LoadTransitiveChainFixture()));

        kanungu.Urls.YouTube.Should().NotBeNull();
        kanungu.Urls.YouTube!.ToString().Should().Contain("YO4eA110hP0");
        kanungu.Urls.Spotify.Should().NotBeNull();
        kanungu.Urls.Spotify!.ToString().Should().Contain("3vk2JpHOhiYH3GTuelvrWq");
    }

    [Fact]
    public void Deduplicate_CosmosDocumentD6d38104_PreservesAllRowsWhenNoDuplicates()
    {
        var raw = LoadCosmosDocumentFixture();
        var deduped = _sut.Deduplicate(raw);

        raw.Should().HaveCount(
            CosmosRawRowCount,
            because: $"Cosmos document {CosmosDocumentId} stores raw discovery rows before read-time dedupe");
        deduped.Should().HaveCount(
            raw.Count,
            because: "unrelated episodes must not collapse when no rows share an episode-level URL");
        deduped.Select(x => x.Id).Should().BeEquivalentTo(raw.Select(x => x.Id));
    }

    [Fact]
    public void Deduplicate_DoubledCosmosDocumentWithPartialUrls_ProducesNinetyFiveResults()
    {
        var raw = LoadCosmosDocumentFixture();
        raw.Should().HaveCount(CosmosRawRowCount);

        var baseline = _sut.Deduplicate(raw);
        baseline.Should().HaveCount(CosmosRawRowCount);

        var random = new Random(42);
        var input = raw.ToList();
        foreach (var original in raw)
        {
            var duplicate = CloneWithNewId(original);
            MaybeStripUrlsForPartialVariant(duplicate, random);
            input.Add(duplicate);
        }

        input.Should().HaveCount(CosmosRawRowCount * 2);

        var results = _sut.Deduplicate(input);

        results.Should().HaveCount(
            CosmosRawRowCount,
            because: "each episode should merge with its partial duplicate without collapsing unrelated rows");

        AssertNoCrossEpisodeContamination(baseline, results, strictBaselineMatch: false);

        results.Select(EpisodeIdentityKey).Should().BeEquivalentTo(
            baseline.Select(EpisodeIdentityKey),
            because: "each logical episode should appear exactly once after deduplication");
    }

    [Fact]
    public void Deduplicate_TransitiveChainRegression_ProducesFiveCanonicalEpisodes()
    {
        var results = _sut.Deduplicate(LoadTransitiveChainFixture());

        results.Should().HaveCount(5);
        results.Should().Contain(x => x.EpisodeName == KanunguEpisodeName);
        results.Should().Contain(x => x.Id == LittleLiterId);
        results.Should().Contain(x => x.Id == ConservativeCultId);
        results.Should().Contain(x => x.Id == ObamaSpeechId);
        results.Should().Contain(x => x.Id == MonsterClubId);

        results.Count(x => x.AutoHidden).Should().Be(5);
        FindKanungu(results).AutoHidden.Should().BeTrue();
    }

    [Fact]
    public void Deduplicate_RowsWithNoSharedUrls_StaySeparate()
    {
        var rowA = CreateResult(
            episodeName: "Episode A",
            showName: "Show A",
            spotify: "https://open.spotify.com/episode/aaaaaaaaaaaaaaaaaaaaaa");

        var rowB = CreateResult(
            episodeName: "Episode B",
            showName: "Show B",
            apple: "https://podcasts.apple.com/podcast/episode-b/id1111111111?i=1000000000001");

        var results = _sut.Deduplicate([rowA, rowB]);

        results.Should().HaveCount(2);
    }

    [Fact]
    public void Deduplicate_DirectUrlMatch_CombinesMissingPlatformsWithoutTransitiveGraft()
    {
        var youTubeRow = CreateResult(
            episodeName: "Same Episode",
            showName: "Same Show",
            youtube: "https://www.youtube.com/watch?v=shared123",
            acceptProbability: 0.2f,
            subjects: ["Subject A"]);

        var spotifyRow = CreateResult(
            episodeName: "Same Episode",
            showName: "Same Show",
            spotify: "https://open.spotify.com/episode/sharedspotify1234567890",
            youtube: "https://www.youtube.com/watch?v=shared123",
            acceptProbability: 0.9f,
            subjects: ["Subject B", "Subject C"]);

        var unrelatedAppleRow = CreateResult(
            episodeName: "Different Episode",
            showName: "Different Show",
            spotify: "https://open.spotify.com/episode/sharedspotify1234567890",
            apple: NbaAppleUrl.ToString(),
            acceptProbability: 0.99f,
            subjects: ["Unrelated"]);

        var results = _sut.Deduplicate([youTubeRow, spotifyRow, unrelatedAppleRow]);

        results.Should().HaveCount(1);
        results[0].Urls.YouTube!.ToString().Should().Contain("shared123");
        results[0].Urls.Spotify!.ToString().Should().Contain("sharedspotify1234567890");
        results[0].Urls.Apple.Should().BeNull();
        results[0].AcceptProbability.Should().Be(0.9f);
        results[0].Subjects.Should().BeEquivalentTo(["Subject B", "Subject C"]);
    }

    [Fact]
    public void Deduplicate_DuplicatedPartialUrlVariantsFromFixture_MergesSafely()
    {
        var fixture = LoadTransitiveChainFixture();
        var youTubeOnly = fixture.Single(x =>
            x.EpisodeName == KanunguEpisodeName &&
            x.Urls.YouTube != null &&
            x.Urls.Spotify == null &&
            x.Urls.Apple == null);
        var spotifyPlusYouTube = fixture.Single(x =>
            x.EpisodeName == KanunguEpisodeName &&
            x.Urls.Spotify != null &&
            x.Urls.YouTube != null);
        var wrongAppleViaSpotify = fixture.Single(x =>
            x.EpisodeName == "Crossover episode: Hornets and Timberwolves trade");

        var otherEpisodes = fixture
            .Where(x => x.EpisodeName != KanunguEpisodeName &&
                        x.EpisodeName != wrongAppleViaSpotify.EpisodeName)
            .ToList();

        var input = new List<DiscoveryResult>
        {
            CloneWithNewId(youTubeOnly),
            CloneWithNewId(spotifyPlusYouTube),
            CloneWithNewId(wrongAppleViaSpotify),
            CloneWithNewId(youTubeOnly),
            CloneWithNewId(spotifyPlusYouTube)
        };
        input.AddRange(otherEpisodes.Select(CloneWithNewId));

        var baseline = _sut.Deduplicate(fixture);
        var results = _sut.Deduplicate(input);

        results.Should().HaveCount(baseline.Count);
        AssertNoCrossEpisodeContamination(baseline, results, strictBaselineMatch: true);

        var kanungu = FindKanungu(results);
        kanungu.Urls.YouTube!.ToString().Should().Contain("YO4eA110hP0");
        kanungu.Urls.Spotify!.ToString().Should().Contain("3vk2JpHOhiYH3GTuelvrWq");
        kanungu.Urls.Apple.Should().BeNull();
        kanungu.AcceptProbability.Should().BeLessThan(0.1f);
        kanungu.Subjects.Should().BeEquivalentTo(["Movement for the Restoration of the Ten Commandments of God"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(42)]
    [InlineData(99)]
    public void Deduplicate_RandomDuplicateSubset_NoCrossEpisodeContamination(int seedIndex)
    {
        var fixture = LoadTransitiveChainFixture();
        var baseline = _sut.Deduplicate(fixture);
        var random = new Random(seedIndex);

        for (var iteration = 0; iteration < FuzzSeedCount; iteration++)
        {
            var input = fixture.ToList();
            var duplicateCount = random.Next(1, fixture.Count + 1);
            var itemsToDuplicate = fixture
                .Where(x => CountUrls(x) > 0)
                .OrderBy(_ => random.Next())
                .Take(duplicateCount)
                .ToList();

            if (itemsToDuplicate.Count == 0)
            {
                continue;
            }

            foreach (var item in itemsToDuplicate)
            {
                var duplicate = CloneWithNewId(item);
                MaybeStripUrlsForPartialVariant(duplicate, random);
                input.Add(duplicate);
            }

            var results = _sut.Deduplicate(input);

            results.Should().HaveCount(
                baseline.Count,
                because: $"seed {seedIndex} iteration {iteration} duplicated {duplicateCount} items from a {fixture.Count}-row fixture");

            AssertNoCrossEpisodeContamination(baseline, results, strictBaselineMatch: false);
        }
    }

    private static DiscoveryResult FindKanungu(IReadOnlyList<DiscoveryResult> results) =>
        results.Single(x => x.EpisodeName == KanunguEpisodeName);

    private static IReadOnlyList<DiscoveryResult> LoadCosmosDocumentFixture()
    {
        using var stream = OpenEmbeddedFixture("DiscoveryResultsDocument_d6d38104.json");
        using var document = JsonDocument.Parse(stream);

        var documentId = document.RootElement.GetProperty("id").GetString();
        if (!string.Equals(documentId, CosmosDocumentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expected Cosmos fixture id '{CosmosDocumentId}' but found '{documentId}'.");
        }

        return document.RootElement
                   .GetProperty("discoveryResults")
                   .Deserialize<List<DiscoveryResult>>(JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize Cosmos discoveryResults.");
    }

    private static IReadOnlyList<DiscoveryResult> LoadTransitiveChainFixture()
    {
        using var stream = OpenEmbeddedFixture("Document_d6d38104_TransitiveChainRegression.json");
        return JsonSerializer.Deserialize<List<DiscoveryResult>>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize transitive-chain regression fixture.");
    }

    private static Stream OpenEmbeddedFixture(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));

        return assembly.GetManifestResourceStream(resourceName)
               ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
    }

    private static DiscoveryResult CreateResult(
        string episodeName,
        string showName,
        string? spotify = null,
        string? apple = null,
        string? youtube = null,
        float? acceptProbability = null,
        IEnumerable<string>? subjects = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            EpisodeName = episodeName,
            ShowName = showName,
            Urls = new DiscoveryResultUrls
            {
                Spotify = spotify == null ? null : new Uri(spotify),
                Apple = apple == null ? null : new Uri(apple),
                YouTube = youtube == null ? null : new Uri(youtube)
            },
            AcceptProbability = acceptProbability,
            Subjects = subjects?.ToArray() ?? [],
            AutoHidden = true
        };

    private static DiscoveryResult CloneWithNewId(DiscoveryResult source) =>
        new()
        {
            Id = Guid.NewGuid(),
            EpisodeName = source.EpisodeName,
            ShowName = source.ShowName,
            Released = source.Released,
            Length = source.Length,
            ShowDescription = source.ShowDescription,
            Description = source.Description,
            State = source.State,
            Urls = new DiscoveryResultUrls
            {
                Spotify = source.Urls.Spotify,
                Apple = source.Urls.Apple,
                YouTube = source.Urls.YouTube
            },
            Subjects = source.Subjects.ToArray(),
            YouTubeViews = source.YouTubeViews,
            YouTubeChannelMembers = source.YouTubeChannelMembers,
            ImageUrl = source.ImageUrl,
            Sources = source.Sources.ToArray(),
            EnrichedTimeFromApple = source.EnrichedTimeFromApple,
            EnrichedUrlFromSpotify = source.EnrichedUrlFromSpotify,
            MatchingPodcastIds = source.MatchingPodcastIds.ToArray(),
            AcceptProbability = source.AcceptProbability,
            AutoHidden = source.AutoHidden
        };

    private static void MaybeStripUrlsForPartialVariant(DiscoveryResult duplicate, Random random)
    {
        var availablePlatforms = new List<Action>();
        if (duplicate.Urls.YouTube != null)
        {
            availablePlatforms.Add(() => duplicate.Urls.YouTube = null);
        }

        if (duplicate.Urls.Spotify != null)
        {
            availablePlatforms.Add(() => duplicate.Urls.Spotify = null);
        }

        if (duplicate.Urls.Apple != null)
        {
            availablePlatforms.Add(() => duplicate.Urls.Apple = null);
        }

        if (availablePlatforms.Count <= 1)
        {
            return;
        }

        var stripCount = random.Next(1, availablePlatforms.Count);
        foreach (var strip in availablePlatforms.OrderBy(_ => random.Next()).Take(stripCount))
        {
            strip();
        }
    }

    private static int CountUrls(DiscoveryResult result) =>
        (result.Urls.Spotify != null ? 1 : 0) +
        (result.Urls.Apple != null ? 1 : 0) +
        (result.Urls.YouTube != null ? 1 : 0);

    private static string EpisodeIdentityKey(DiscoveryResult result) =>
        $"{result.ShowName}|{result.EpisodeName}";

    private static void AssertNoCrossEpisodeContamination(
        IReadOnlyList<DiscoveryResult> baseline,
        IReadOnlyList<DiscoveryResult> actual,
        bool strictBaselineMatch)
    {
        actual.Should().HaveCount(baseline.Count);

        var actualKeys = actual.Select(EpisodeIdentityKey).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in baseline)
        {
            actualKeys.Should().Contain(
                EpisodeIdentityKey(expected),
                because: "each canonical episode should remain after deduplication");

            if (!strictBaselineMatch)
            {
                continue;
            }

            var match = actual.Single(x => EpisodeIdentityKey(x) == EpisodeIdentityKey(expected));

            match.ShowName.Should().Be(expected.ShowName);
            match.EpisodeName.Should().Be(expected.EpisodeName);
            match.Subjects.Should().BeEquivalentTo(expected.Subjects);
            match.AcceptProbability.Should().Be(expected.AcceptProbability);
            match.AutoHidden.Should().Be(expected.AutoHidden);

            NormalizeUrl(match.Urls.Spotify).Should().Be(NormalizeUrl(expected.Urls.Spotify));
            NormalizeUrl(match.Urls.Apple).Should().Be(NormalizeUrl(expected.Urls.Apple));
            NormalizeUrl(match.Urls.YouTube).Should().Be(NormalizeUrl(expected.Urls.YouTube));
        }

        actual.Should().NotContain(x =>
            x.EpisodeName == KanunguEpisodeName &&
            x.Urls.Apple != null &&
            x.Urls.Apple.ToString().Contains("hornets-and-timberwolves", StringComparison.OrdinalIgnoreCase));

        actual.Should().NotContain(x =>
            x.EpisodeName == KanunguEpisodeName &&
            x.Subjects.Any(s =>
                string.Equals(s, "Peoples Temple", StringComparison.Ordinal) ||
                string.Equals(s, "Theranos", StringComparison.Ordinal) ||
                string.Equals(s, "Scientology", StringComparison.Ordinal)));

        var baselineKanungu = baseline.FirstOrDefault(x => x.EpisodeName == KanunguEpisodeName);
        if (baselineKanungu is { AcceptProbability: <= 0.1f })
        {
            actual.Should().NotContain(x =>
                x.EpisodeName == KanunguEpisodeName &&
                x.AcceptProbability > 0.1f);
        }
        else if (baselineKanungu != null)
        {
            var actualKanungu = actual.Single(x => x.EpisodeName == KanunguEpisodeName);
            (actualKanungu.AcceptProbability ?? 0f).Should().BeLessThanOrEqualTo(
                baselineKanungu.AcceptProbability ?? 0f,
                because: "deduplication must not inflate Kanungu accept probability via cross-episode merge");
        }
    }

    private static string? NormalizeUrl(Uri? url) =>
        url?.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}
