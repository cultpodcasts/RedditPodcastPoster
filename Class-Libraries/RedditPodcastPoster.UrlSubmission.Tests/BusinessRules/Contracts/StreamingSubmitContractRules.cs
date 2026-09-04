using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.Contracts;

/// <summary>
/// Cross-repo streaming-submit contract: JSON published by Api, copied under docs/contracts.
/// Production membership <c>service</c> field lands in a later PR; this locks ServiceKeys parity first.
/// </summary>
public class StreamingSubmitContractRules
{
    private static readonly JsonDocument Contract = LoadContract();

    [Fact(DisplayName =
        "Streaming-submit contract JSON lists exactly ServiceCatalog.SearchEncodedKeys, because wire service enums must match RPP ServiceKeys.")]
    public void streaming_contract_service_keys_match_search_encoded_keys()
    {
        // Arrange
        var fromContract = Contract.RootElement
            .GetProperty("streamingServiceKeys")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        // Act
        var fromCatalog = ServiceCatalog.SearchEncodedKeys;

        // Assert
        fromContract.Should().Equal(fromCatalog);
    }

    [Fact(DisplayName =
        "Streaming-submit contract excludes Spotify, Apple, and YouTube, because podcast-service platforms use APIs not this scrape/prepare path.")]
    public void streaming_contract_excludes_podcast_service_keys()
    {
        // Arrange
        var fromContract = Contract.RootElement
            .GetProperty("streamingServiceKeys")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        // Act / Assert
        fromContract.Should().NotContain(ServiceKeys.Spotify);
        fromContract.Should().NotContain(ServiceKeys.Apple);
        fromContract.Should().NotContain(ServiceKeys.YouTube);
        fromContract.Should().Contain(ServiceKeys.Itvx);
        fromContract.Should().Contain(ServiceKeys.DiscoveryPlus);
    }

    [Fact(DisplayName =
        "Streaming-submit contract default Browser Rendering allowlist is itvx only, because CF env starts with the known Azure-blocked host.")]
    public void streaming_contract_default_browser_rendering_is_itvx()
    {
        // Arrange
        var allow = Contract.RootElement
            .GetProperty("defaultBrowserRenderingServices")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        // Act / Assert
        allow.Should().Equal(ServiceKeys.Itvx);
    }

    [Fact(DisplayName =
        "Streaming-submit contract rules flag membershipReturnsService and membershipDoesNotScrape, because lookup classifies only and prepare owns HTML fetch.")]
    public void streaming_contract_documents_membership_vs_prepare_split()
    {
        // Arrange
        var rules = Contract.RootElement.GetProperty("rules");

        // Act / Assert
        rules.GetProperty("membershipReturnsService").GetBoolean().Should().BeTrue();
        rules.GetProperty("membershipDoesNotScrape").GetBoolean().Should().BeTrue();
        rules.GetProperty("prepareFetchesHtml").GetBoolean().Should().BeTrue();
        rules.GetProperty("submitUsesPrefetchedMetaWhenCached").GetBoolean().Should().BeTrue();
        rules.GetProperty("azureDoesNotCallCloudflare").GetBoolean().Should().BeTrue();
        rules.GetProperty("podcastServicesOutOfScope").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName =
        "Streaming-submit contract includes membership and orchestration case ids for every streaming service, because permutations must stay complete.")]
    public void streaming_contract_case_ids_cover_every_service()
    {
        // Arrange
        var keys = Contract.RootElement
            .GetProperty("streamingServiceKeys")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        var membershipIds = Contract.RootElement
            .GetProperty("streamingMembershipShapeCaseIds")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        var orchestrationIds = Contract.RootElement
            .GetProperty("streamingOrchestrationCaseIds")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        // Act / Assert
        membershipIds.Should().HaveCount(keys.Length * 3);
        orchestrationIds.Should().HaveCount(keys.Length);
        foreach (var key in keys)
        {
            membershipIds.Should().Contain($"membership-{key}-known");
            membershipIds.Should().Contain($"membership-{key}-unknown");
            membershipIds.Should().Contain($"membership-{key}-ambiguous");
            orchestrationIds.Should().Contain($"stream-{key}-unknown-prepare-submit");
        }
    }

    private static JsonDocument LoadContract()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "contracts", "streaming-submit-contract.json");
        File.Exists(path).Should().BeTrue(
            $"expected copied contract at {path}; ensure UrlSubmission.Tests copies docs/contracts JSON to output");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
