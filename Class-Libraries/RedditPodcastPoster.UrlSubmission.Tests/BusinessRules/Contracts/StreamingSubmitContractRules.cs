using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.PodcastServices.Extensions;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.Contracts;

/// <summary>
/// Cross-repo streaming-submit contract: JSON published by Api, copied under docs/contracts.
/// Locks JSON ↔ <see cref="StreamingServiceCatalog.SearchEncodedKeys"/> (and rule/case-id completeness)
/// alongside the membership <c>service</c> field shipped in this PR.
/// </summary>
public class StreamingSubmitContractRules
{
    private static readonly JsonDocument Contract = LoadContract();

    [Fact(DisplayName =
        "Streaming-submit contract streamingServiceKeys == SubmitEligibleKeys == SearchEncodedKeys " +
        "(AllKeys minus submit-retired). ImageCoalesceKeys / AllKeys may still include retired services " +
        "(e.g. Hulu) for historical Cosmos URLs; Hulu is submit-retired — no episode catalogue pages.")]
    public void streaming_contract_service_keys_match_submit_eligible_and_search_encoded_keys()
    {
        // Arrange
        var fromContract = Contract.RootElement
            .GetProperty("streamingServiceKeys")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        // Act
        var fromCatalog = StreamingServiceCatalog.SearchEncodedKeys;
        var submitEligible = StreamingServiceWire.SubmitEligibleKeys;
        var allKeys = StreamingServiceWire.AllKeys;
        var huluKey = StreamingServiceWire.ToKey(StreamingService.Hulu);

        // Assert
        fromContract.Should().Equal(submitEligible);
        fromCatalog.Should().Equal(submitEligible);
        fromContract.Should().NotContain(huluKey);
        allKeys.Should().Contain(huluKey);
        StreamingServiceWire.IsSubmitRetired(StreamingService.Hulu).Should().BeTrue();
        StreamingServiceWire.ImageCoalesceKeys.Should().Contain(huluKey);
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
        var containsItvx = fromContract.Contains(StreamingServiceWire.ToKey(StreamingService.Itvx));
        var containsDiscoveryPlus = fromContract.Contains(StreamingServiceWire.ToKey(StreamingService.DiscoveryPlus));

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
        var expected = new[] { StreamingServiceWire.ToKey(StreamingService.Itvx) };

        // Assert
        allow.Should().Equal(expected);
    }

    [Fact(DisplayName =
        "Streaming-submit contract scrapeProfiles lock Peacock only to directHttp in us, because US geo soft-walls use regional fetch not Browser Rendering; Hulu is submit-retired.")]
    public void streaming_contract_scrape_profiles_peacock_is_us_direct_http()
    {
        // Arrange
        var regions = Contract.RootElement
            .GetProperty("scrapeRegions")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        var profiles = Contract.RootElement.GetProperty("scrapeProfiles");
        var huluKey = StreamingServiceWire.ToKey(StreamingService.Hulu);
        var peacockKey = StreamingServiceWire.ToKey(StreamingService.Peacock);

        // Act
        var peacock = profiles.GetProperty(peacockKey);
        var profileNames = profiles.EnumerateObject().Select(p => p.Name).ToArray();

        // Assert
        regions.Should().Contain("default");
        regions.Should().Contain("us");
        profileNames.Should().Equal(peacockKey);
        peacock.GetProperty("mode").GetString().Should().Be("directHttp");
        peacock.GetProperty("region").GetString().Should().Be("us");
        profileNames.Should().NotContain(huluKey);
    }

    [Fact(DisplayName =
        "Streaming-submit contract scrapeProfiles and defaultBrowserRenderingServices adapters register ExtractMetaData(html), " +
        "so Worker SCRAPE_US / Browser Rendering prepare does not hit HTML extract is not registered.")]
    public async Task streaming_contract_scrape_and_br_services_register_html_extract()
    {
        // Arrange
        var profileKeys = Contract.RootElement
            .GetProperty("scrapeProfiles")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToArray();
        var brKeys = Contract.RootElement
            .GetProperty("defaultBrowserRenderingServices")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        var keys = profileKeys.Concat(brKeys).Distinct(StringComparer.Ordinal).ToArray();
        var specimens = Contract.RootElement.GetProperty("streamingSpecimenUrls");
        var html = "<html><head><meta property=\"og:title\" content=\"Contract Prefetch Title\" /></head></html>";
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddNonPodcastScrapers();
        using var provider = services.BuildServiceProvider();
        var adapters = provider.GetServices<INonPodcastServiceAdapter>().ToArray();

        foreach (var key in keys)
        {
            var url = new Uri(specimens.GetProperty(key).GetString()!);
            var adapter = adapters.Single(candidate => candidate.IsSubmitUrl(url));

            // Act
            var act = async () => await adapter.ExtractMetaData(url, html);

            // Assert
            await act.Should().NotThrowAsync<NotSupportedException>(
                because: $"service '{key}' is in scrapeProfiles or defaultBrowserRenderingServices " +
                         "and must register extractFromHtml on CatalogKeyedNonPodcastServiceAdapter");
            var meta = await adapter.ExtractMetaData(url, html);
            meta.Title.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact(DisplayName =
        "Streaming-submit contract flags membershipReturnsService and membershipDoesNotScrape as true now, because membership classifies only and prepare owns HTML fetch.")]
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
            "membershipDoesNotScrape is live: membership must not scrape; prepare owns HTML fetch");
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
