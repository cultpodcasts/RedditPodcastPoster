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
        "When building the verify note, then it names the production URL and the route's X-…-Cache header and says to expect MISS.")]
    public void VerifyNote()
    {
        var result = Sut().Build([ApiCachedContent.SearchSuggestions])!;

        result.VerifyNote.Should()
            .Contain("https://api.cultpodcasts.com/search-suggestions")
            .And.Contain("X-Search-Suggestions-Cache")
            .And.Contain("MISS");
    }

    [Fact(DisplayName =
        "When formatting for the console, then the block contains a clear ACTION banner, each command on its own line and the verify note.")]
    public void Format_Block()
    {
        var sut = Sut();
        var result = sut.Build([ApiCachedContent.Homepage, ApiCachedContent.SearchSuggestions])!;

        var lines = sut.Format(result).Split(Environment.NewLine);

        lines.Should().Contain(l => l.StartsWith("ACTION: purge the Api edge cache"));
        lines.Should().Contain(result.Commands[0]);
        lines.Should().Contain(result.VerifyNote);
    }
}
