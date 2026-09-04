using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.Contracts;

/// <summary>
/// Cross-repo streaming-submit contract: JSON published by Api, copied under docs/contracts.
/// Locks JSON ↔ <see cref="ServiceCatalog.SearchEncodedKeys"/> (and rule/case-id completeness)
/// alongside the membership <c>service</c> field shipped in this PR.
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

        // Act
        var containsSpotify = fromContract.Contains(ServiceKeys.Spotify);
        var containsApple = fromContract.Contains(ServiceKeys.Apple);
        var containsYouTube = fromContract.Contains(ServiceKeys.YouTube);
        var containsItvx = fromContract.Contains(ServiceKeys.Itvx);
        var containsDiscoveryPlus = fromContract.Contains(ServiceKeys.DiscoveryPlus);

        // Assert
        containsSpotify.Should().BeFalse();
        containsApple.Should().BeFalse();
        containsYouTube.Should().BeFalse();
        containsItvx.Should().BeTrue();
        containsDiscoveryPlus.Should().BeTrue();
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

        // Act
        var expected = new[] { ServiceKeys.Itvx };

        // Assert
        allow.Should().Equal(expected);
    }

    [Fact(DisplayName =
        "Streaming-submit contract flags membershipReturnsService as true now and membershipDoesNotScrape as the target after prepare owns HTML fetch, because today membership may still extract show name.")]
    public void streaming_contract_documents_membership_vs_prepare_split()
    {
        // Arrange
        var rules = Contract.RootElement.GetProperty("rules");

        // Act
        var membershipReturnsService = rules.GetProperty("membershipReturnsService").GetBoolean();
        var membershipDoesNotScrape = rules.GetProperty("membershipDoesNotScrape").GetBoolean();
        var prepareFetchesHtml = rules.GetProperty("prepareFetchesHtml").GetBoolean();
        var submitUsesPrefetchedMetaWhenCached =
            rules.GetProperty("submitUsesPrefetchedMetaWhenCached").GetBoolean();
        var azureDoesNotCallCloudflare = rules.GetProperty("azureDoesNotCallCloudflare").GetBoolean();
        var podcastServicesOutOfScope = rules.GetProperty("podcastServicesOutOfScope").GetBoolean();

        // Assert
        membershipReturnsService.Should().BeTrue();
        membershipDoesNotScrape.Should().BeTrue(
            "membershipDoesNotScrape is the target orchestration state after prepare lands; " +
            "api-infra membership may still ExtractMetaData for unknown streaming URLs until then");
        prepareFetchesHtml.Should().BeTrue();
        submitUsesPrefetchedMetaWhenCached.Should().BeTrue();
        azureDoesNotCallCloudflare.Should().BeTrue();
        podcastServicesOutOfScope.Should().BeTrue();
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

        // Act
        var expectedMembershipCount = keys.Length * 3;
        var expectedOrchestrationCount = keys.Length;

        // Assert
        membershipIds.Should().HaveCount(expectedMembershipCount);
        orchestrationIds.Should().HaveCount(expectedOrchestrationCount);
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
