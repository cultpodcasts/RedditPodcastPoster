using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Cloudflare.Clients;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.CloudflareRedirect.Configuration;
using RedditPodcastPoster.CloudflareRedirect.Models;
using RedditPodcastPoster.CloudflareRedirect.Services;
using Xunit;

namespace FunctionHost.Tests.Redirects;

public class CatalogueRedirectRules
{
    private const string Namespace = "synthetic-redirect-namespace";
    private readonly Mock<IKVClient> _kv = new();
    private readonly Dictionary<string, string> _existing = [];
    private readonly List<KVRecord> _writes = [];

    public CatalogueRedirectRules()
    {
        _kv.Setup(k => k.GetAll(Namespace)).ReturnsAsync(() => _existing);
        _kv.Setup(k => k.Write(It.IsAny<KVRecord>(), Namespace))
            .Callback<KVRecord, string>((record, _) => _writes.Add(record))
            .ReturnsAsync(new WriteResult(true));
    }

    private RedirectService Service => new(
        _kv.Object,
        Options.Create(new RedirectOptions { KVRedirectNamespaceId = Namespace }),
        NullLogger<RedirectService>.Instance);

    [Theory(DisplayName =
        "A catalogue redirect is written to the podcast redirect KV namespace as '{route root}/{old}' -> '{new}', " +
        "so the website can look it up from the path, and it never collides with a podcast key (which has no '/').")]
    [InlineData(CatalogueRedirectKind.NewsOrganisation, "news/Synthetic Old")]
    [InlineData(CatalogueRedirectKind.TvShow, "tv/Synthetic Old")]
    [InlineData(CatalogueRedirectKind.Film, "film/Synthetic Old")]
    public async Task writes_prefixed_key(CatalogueRedirectKind kind, string key)
    {
        // Act
        var created = await Service.CreateCatalogueRedirect(new CatalogueRedirect(kind, "Synthetic Old", "Synthetic New"));

        // Assert
        created.Should().BeTrue();
        _writes.Should().ContainSingle();
        _writes[0].Key.Should().Be(key);
        _writes[0].Value.Should().Be("Synthetic New");
    }

    [Fact(DisplayName =
        "Like the podcast loop guard, a redirect is refused (nothing written) when one of the same kind already starts at the new name, in any case.")]
    public async Task refuses_loop()
    {
        // Arrange
        _existing["tv/synthetic new"] = "Synthetic Old";

        // Act
        var created = await Service.CreateCatalogueRedirect(
            new CatalogueRedirect(CatalogueRedirectKind.TvShow, "Synthetic Old", "Synthetic New"));

        // Assert
        created.Should().BeFalse();
        _writes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Redirects of other kinds, including a podcast redirect from the same name, do not block a catalogue redirect.")]
    public async Task other_kinds_do_not_block()
    {
        // Arrange
        _existing["Synthetic New"] = "Synthetic Podcast";
        _existing["news/Synthetic New"] = "Synthetic Org";

        // Act
        var created = await Service.CreateCatalogueRedirect(
            new CatalogueRedirect(CatalogueRedirectKind.TvShow, "Synthetic Old", "Synthetic New"));

        // Assert
        created.Should().BeTrue();
        _writes.Single().Key.Should().Be("tv/Synthetic Old");
    }

    [Fact(DisplayName =
        "Writing the same redirect again (a retried rename) is allowed and idempotent; a failed KV write throws so the rename fails before anything is saved.")]
    public async Task retry_and_write_failure()
    {
        // Arrange
        _existing["film/Synthetic Old"] = "Synthetic New";

        // Act
        var again = await Service.CreateCatalogueRedirect(
            new CatalogueRedirect(CatalogueRedirectKind.Film, "Synthetic Old", "Synthetic New"));
        _kv.Setup(k => k.Write(It.IsAny<KVRecord>(), Namespace)).ReturnsAsync(new WriteResult(false));
        var failing = () => Service.CreateCatalogueRedirect(
            new CatalogueRedirect(CatalogueRedirectKind.Film, "Synthetic Old", "Synthetic New"));

        // Assert
        again.Should().BeTrue();
        await failing.Should().ThrowAsync<InvalidOperationException>();
    }
}
