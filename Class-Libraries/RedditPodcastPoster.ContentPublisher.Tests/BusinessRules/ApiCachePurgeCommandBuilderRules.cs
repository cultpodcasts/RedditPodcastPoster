using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.ContentPublisher.ApiCache;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class ApiCachePurgeCommandBuilderRules
{
    private readonly AutoMocker _mocker = new();

    private ApiCachePurgeCommandBuilder Sut() => _mocker.CreateInstance<ApiCachePurgeCommandBuilder>();

    [Fact(DisplayName =
        "When nothing Api-cached was published, then no purge instructions are built, because languages/people/subjects/flairs are not edge-cached.")]
    public void NothingPublished_ReturnsNull()
    {
        Sut().Build([]).Should().BeNull();
    }

    [Fact(DisplayName =
        "When search-suggestions was published, then exactly the production and preview /search-suggestions URLs are purged, because both hosts cache the same R2 key under their full URL.")]
    public void SearchSuggestions_Urls()
    {
        var result = Sut().Build([ApiCachedContent.SearchSuggestions])!;

        result.Urls.Should().Equal(
            "https://api.cultpodcasts.com/search-suggestions",
            "https://api-preview.cultpodcasts.com/search-suggestions");
    }

    [Fact(DisplayName =
        "When the homepage was published, then exactly the production and preview /homepage URLs are purged.")]
    public void Homepage_Urls()
    {
        var result = Sut().Build([ApiCachedContent.Homepage])!;

        result.Urls.Should().Equal(
            "https://api.cultpodcasts.com/homepage",
            "https://api-preview.cultpodcasts.com/homepage");
    }

    [Fact(DisplayName =
        "When both were published (duplicates included), then four distinct URLs go in one command, because the batch limit is far above four.")]
    public void Both_DistinctUrls_OneCommand()
    {
        var result = Sut().Build([
            ApiCachedContent.SearchSuggestions, ApiCachedContent.Homepage, ApiCachedContent.SearchSuggestions
        ])!;

        result.Urls.Should().HaveCount(4).And.OnlyHaveUniqueItems();
        result.Commands.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When building the command, then it is the PowerShell cf.cmd form with backslash-escaped JSON quotes, zone cultpodcasts.com and --force, because unescaped quotes are stripped by the .cmd shim.")]
    public void Command_ExactPowerShellShape()
    {
        var result = Sut().Build([ApiCachedContent.SearchSuggestions])!;

        result.Commands.Should().ContainSingle().Which.Should().Be(
            "cf cache purge -z cultpodcasts.com --body " +
            "'{\\\"files\\\":[\\\"https://api.cultpodcasts.com/search-suggestions\\\",\\\"https://api-preview.cultpodcasts.com/search-suggestions\\\"]}' --force");
    }

    [Fact(DisplayName =
        "When there are more URLs than the per-request limit, then they are split into batches of at most MaxUrlsPerCommand, keeping every URL once.")]
    public void Batching()
    {
        var urls = Enumerable.Range(1, ApiCachePurgeCommandBuilder.MaxUrlsPerCommand * 2 + 1)
            .Select(i => $"https://synthetic.example.invalid/item-{i}")
            .ToList();

        var commands = ApiCachePurgeCommandBuilder.BuildCommands(urls);

        commands.Should().HaveCount(3);
        commands.Sum(c => urls.Count(u => c.Contains($"\\\"{u}\\\""))).Should().Be(urls.Count);
        commands[2].Should().Contain("item-61").And.NotContain("item-60\\\"");
    }

    [Fact(DisplayName =
        "When only search-suggestions was published, then there is one verify line naming its production URL, X-Search-Suggestions-Cache and MISS, then the browser-cache caveat.")]
    public void VerifyLines_Single()
    {
        var result = Sut().Build([ApiCachedContent.SearchSuggestions])!;

        result.VerifyLines.Should().HaveCount(2);
        result.VerifyLines[0].Should()
            .StartWith("Verify: ")
            .And.Contain("https://api.cultpodcasts.com/search-suggestions")
            .And.Contain("X-Search-Suggestions-Cache")
            .And.Contain("MISS");
        result.VerifyLines[1].Should().Be(ApiCachePurgeCommandBuilder.BrowserCacheNote);
    }

    [Fact(DisplayName =
        "When both were published, then every purged route gets its own verify line with its own header, because checking only /homepage would miss the 22 Oct /search-suggestions case.")]
    public void VerifyLines_EveryRoute()
    {
        var result = Sut().Build([ApiCachedContent.Homepage, ApiCachedContent.SearchSuggestions])!;

        var verify = result.VerifyLines.Where(l => l.StartsWith("Verify: ")).ToList();
        verify.Should().HaveCount(2);
        verify.Should().ContainSingle(l =>
            l.Contains("https://api.cultpodcasts.com/homepage ") && l.Contains("'X-Homepage-Cache'"));
        verify.Should().ContainSingle(l =>
            l.Contains("https://api.cultpodcasts.com/search-suggestions ") && l.Contains("'X-Search-Suggestions-Cache'"));
        result.VerifyLines.Should().EndWith(ApiCachePurgeCommandBuilder.BrowserCacheNote);
    }

    [Fact(DisplayName =
        "When building the browser-cache caveat, then it says the purge does not clear browser caches and mentions the 4 h zone Browser Cache TTL.")]
    public void BrowserCacheNote_Content()
    {
        ApiCachePurgeCommandBuilder.BrowserCacheNote.Should()
            .Contain("does not clear browser caches")
            .And.Contain("4 h");
    }

    [Fact(DisplayName =
        "When formatting for the console, then the block contains a clear ACTION banner, each command on its own line and every verify line.")]
    public void Format_Block()
    {
        var sut = Sut();
        var result = sut.Build([ApiCachedContent.Homepage, ApiCachedContent.SearchSuggestions])!;

        var lines = sut.Format(result).Split(Environment.NewLine);

        lines.Should().Contain(l => l.StartsWith("ACTION: purge the Api edge cache"));
        lines.Should().Contain(result.Commands[0]);
        lines.Should().Contain(result.VerifyLines);
    }
}
