#Requires -Version 7
<#
.SYNOPSIS
  Scaffold a Channel4-shaped streaming service plugin under Class-Libraries/.

.DESCRIPTION
  Creates RedditPodcastPoster.<Pascal> + .Tests with host-only matcher stub,
  Open Graph extractor, DI Add*Services, and StreamingService registration.
  Does NOT wire keys / KnownStreamingServices / Api / website — prints checklist.

.EXAMPLE
  pwsh ./scripts/scaffold-streaming-service.ps1 `
    -Key franceTv `
    -DisplayName "France TV" `
    -Hosts france.tv `
    -Icon france-tv
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-zA-Z0-9]*$')]
    [string] $Key,

    [Parameter(Mandatory)]
    [string] $DisplayName,

    [Parameter(Mandatory)]
    [string[]] $Hosts,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')]
    [string] $Icon,

    [string] $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-PascalCase([string] $camel) {
    if ([string]::IsNullOrWhiteSpace($camel)) {
        throw 'Key is empty.'
    }
    return ([char]::ToUpperInvariant($camel[0]) + $camel.Substring(1))
}

function ConvertTo-CSharpStringArray([string[]] $items) {
    ($items | ForEach-Object { "`"$_`"" }) -join ', '
}

function Write-Utf8File([string] $Path, [string] $Content) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n").Replace("`n", "`r`n"))
}

function Expand-Template([string] $template, [hashtable] $map) {
    $result = $template
    foreach ($pair in $map.GetEnumerator()) {
        $result = $result.Replace($pair.Key, [string]$pair.Value)
    }
    return $result
}

$pascal = ConvertTo-PascalCase $Key
$libName = "RedditPodcastPoster.$pascal"
$testName = "$libName.Tests"
$libDir = Join-Path $RepoRoot "Class-Libraries\$libName"
$testDir = Join-Path $RepoRoot "Class-Libraries\$testName"
$hostArrayLiteral = ConvertTo-CSharpStringArray $Hosts
$primaryHost = $Hosts[0]

if (Test-Path $libDir) { throw "Library already exists: $libDir" }
if (Test-Path $testDir) { throw "Test project already exists: $testDir" }

$map = @{
    '__PASCAL__'       = $pascal
    '__KEY__'          = $Key
    '__DISPLAY__'      = $DisplayName
    '__ICON__'         = $Icon
    '__HOSTS_ARRAY__'  = $hostArrayLiteral
    '__PRIMARY_HOST__' = $primaryHost
    '__LIB__'          = $libName
}

$libCsproj = @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RedditPodcastPoster.OpenGraph\RedditPodcastPoster.OpenGraph.csproj" />
    <ProjectReference Include="..\RedditPodcastPoster.PodcastServices.Abstractions\RedditPodcastPoster.PodcastServices.Abstractions.csproj" />
  </ItemGroup>

</Project>
'@

$testCsproj = @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageReference Include="Moq.AutoMock" Version="4.0.2" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\__LIB__\__LIB__.csproj" />
    <ProjectReference Include="..\RedditPodcastPoster.Episodes.TestSupport\RedditPodcastPoster.Episodes.TestSupport.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
'@

$registration = @'
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.__PASCAL__;

public static class __PASCAL__StreamingService
{
    public const string Key = StreamingServiceKeys.__PASCAL__;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "__DISPLAY__",
        "__ICON__",
        true,
        [__HOSTS_ARRAY__]);
}
'@

$matcher = @'
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.__PASCAL__.Matching;

public static class __PASCAL__UrlMatcher
{
    /// <summary>
    /// Host-only stub from scaffold. Tighten path grammar before shipping.
    /// </summary>
    public static bool IsSubmitUrl(Uri url)
    {
        var host = ServiceCatalog.CanonicalHost(url);
        var allowed = new[] { __HOSTS_ARRAY__ };
        if (!allowed.Any(candidate => ServiceCatalog.IsHost(host, candidate)))
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]);
    }
}
'@

$extractor = @'
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.__PASCAL__.Extractors;

public interface I__PASCAL__PageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}

public class __PASCAL__PageMetaDataExtractor(
    IHttpClientFactory httpClientFactory,
    OpenGraphPageMetaDataExtractor openGraphPageMetaDataExtractor
) : I__PASCAL__PageMetaDataExtractor
{
    public const string Publisher = "__DISPLAY__";

    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var client = httpClientFactory.CreateClient(nameof(__PASCAL__PageMetaDataExtractor));
        using var pageResponse = await client.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode);
        }

        var html = await pageResponse.Content.ReadAsStringAsync();
        NonPodcastServiceItemMetaData? openGraph = null;
        try
        {
            using var buffered = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
            openGraph = await openGraphPageMetaDataExtractor.Extract(url, buffered, Publisher);
        }
        catch (NonPodcastServiceMetaDataExtractionException)
        {
            // Soft-walled / non-catalogue shells often omit og:title; fall through to HTML recovery.
        }

        return __PASCAL__CatalogMeta.Merge(url, html, openGraph);
    }
}

internal static partial class __PASCAL__CatalogMeta
{
    public static NonPodcastServiceItemMetaData Merge(
        Uri url,
        string html,
        NonPodcastServiceItemMetaData? openGraph)
    {
        var title = CleanTitle(openGraph?.Title ?? FirstGroup(html, DocumentTitleRegex()));
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NonPodcastServiceMetaDataExtractionException(
                url,
                "__DISPLAY__ page has neither og:title nor a usable document title.");
        }

        var showName = openGraph?.ShowName;
        if (IsMovie(html))
        {
            showName = null;
        }

        if (string.Equals(showName, __PASCAL__PageMetaDataExtractor.Publisher, StringComparison.OrdinalIgnoreCase))
        {
            showName = null;
        }

        return new NonPodcastServiceItemMetaData(
            title,
            openGraph?.Description ?? string.Empty,
            openGraph?.Duration,
            openGraph?.Release,
            openGraph?.Image,
            openGraph?.Explicit,
            __PASCAL__PageMetaDataExtractor.Publisher,
            showName);
    }

    public static bool IsMovie(string html)
    {
        var ogType = FirstGroup(html, OgTypeRegex());
        if (string.Equals(ogType, "video.movie", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ogType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var catalogue = CataloguePrimaryTypeRegex().Match(html);
        return catalogue.Success &&
               catalogue.Groups[1].Value.Equals("Movie", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var title = WebUtility.HtmlDecode(raw).Trim();
        foreach (var suffix in new[] { " | __DISPLAY__", " - __DISPLAY__" })
        {
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[..^suffix.Length].Trim();
            }
        }

        return title;
    }

    private static string? FirstGroup(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentTitleRegex();

    [GeneratedRegex("(?:property|name)=\"og:type\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgTypeRegex();

    [GeneratedRegex("\"@type\"\\s*:\\s*\"(TVSeries|Movie)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CataloguePrimaryTypeRegex();
}
'@

$di = @'
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.__PASCAL__.Extractors;
using RedditPodcastPoster.__PASCAL__.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.__PASCAL__.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add__PASCAL__Services(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(__PASCAL__PageMetaDataExtractor), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        });

        return services
            .AddOpenGraphExtractor()
            .AddScoped<I__PASCAL__PageMetaDataExtractor, __PASCAL__PageMetaDataExtractor>()
            .AddScoped<INonPodcastServiceAdapter>(provider =>
                new CatalogKeyedNonPodcastServiceAdapter(
                    NonPodcastService.__PASCAL__,
                    StreamingServiceKeys.__PASCAL__,
                    __PASCAL__UrlMatcher.IsSubmitUrl,
                    __PASCAL__UrlMatcher.IsSubmitUrl,
                    provider.GetRequiredService<I__PASCAL__PageMetaDataExtractor>().GetMetaData));
    }
}
'@

$matcherTests = @'
using FluentAssertions;
using RedditPodcastPoster.__PASCAL__.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.__PASCAL__.Tests.BusinessRules;

public class __PASCAL__UrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A __DISPLAY__ catalogue URL on an allowed host is a submit URL (scaffold host stub — tighten path rules before ship).")]
    public void allowed_host_path_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.__PRIMARY_HOST__/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = __PASCAL__UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters __PRIMARY_HOST__ is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.__PRIMARY_HOST__.example.test/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = __PASCAL__UrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
'@

$extractorTests = @'
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.__PASCAL__.Extensions;
using RedditPodcastPoster.__PASCAL__.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.OpenGraph.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.__PASCAL__.Tests.BusinessRules;

public class __PASCAL__PageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly AutoMocker _mocker = new();

    public __PASCAL__PageMetaDataExtractorRules()
    {
        _mocker.Use(new OpenGraphPageMetaDataExtractor());
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact(DisplayName =
        "__DISPLAY__ page extract GETs the catalogue URL and reads Open Graph fields, " +
        "so submit can ingest a __DISPLAY__ page as a non-podcast episode.")]
    public async Task extracts_open_graph_from_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var url = new Uri($"https://www.__PRIMARY_HOST__/{_fixture.CreateYouTubeId()}");
        _handler.Response = OkHtml(
            $"<html><head><meta property=\"og:title\" content=\"{title}\" /></head></html>");
        var sut = _mocker.CreateInstance<__PASCAL__PageMetaDataExtractor>();

        // Act
        var meta = await sut.GetMetaData(url);

        // Assert
        meta.Title.Should().Be(title);
        meta.Publisher.Should().Be("__DISPLAY__");
        meta.ShowName.Should().BeNull();
        _handler.LastRequestUri.Should().Be(url);
    }

    [Fact(DisplayName =
        "__DISPLAY__ page extract fails when the HTTP status is not OK, because the page cannot be scraped.")]
    public async Task non_ok_status_fails_extract()
    {
        // Arrange
        var url = new Uri($"https://www.__PRIMARY_HOST__/{_fixture.CreateYouTubeId()}");
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = _mocker.CreateInstance<__PASCAL__PageMetaDataExtractor>();

        // Act
        var act = async () => await sut.GetMetaData(url);

        // Assert
        await act.Should().ThrowAsync<NonPodcastServiceMetaDataExtractionException>();
    }

    [Fact(DisplayName =
        "Add__PASCAL__Services registers a catalog-keyed adapter for __DISPLAY__ URLs, so Open Graph parsing stays in the shared OpenGraph assembly.")]
    public void add_services_registers_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Add__PASCAL__Services();
        using var provider = services.BuildServiceProvider();
        var url = new Uri($"https://www.__PRIMARY_HOST__/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = provider.GetServices<INonPodcastServiceAdapter>()
            .Single(candidate => candidate.IsSubmitUrl(url));

        // Assert
        adapter.Service.Should().Be(NonPodcastService.__PASCAL__);
    }

    private static HttpResponseMessage OkHtml(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
'@

Write-Utf8File (Join-Path $libDir "$libName.csproj") (Expand-Template $libCsproj $map)
Write-Utf8File (Join-Path $testDir "$testName.csproj") (Expand-Template $testCsproj $map)
Write-Utf8File (Join-Path $libDir "${pascal}StreamingService.cs") (Expand-Template $registration $map)
Write-Utf8File (Join-Path $libDir "Matching\${pascal}UrlMatcher.cs") (Expand-Template $matcher $map)
Write-Utf8File (Join-Path $libDir "Extractors\${pascal}PageMetaDataExtractor.cs") (Expand-Template $extractor $map)
Write-Utf8File (Join-Path $libDir "Extensions\ServiceCollectionExtensions.cs") (Expand-Template $di $map)
Write-Utf8File (Join-Path $testDir "BusinessRules\${pascal}UrlMatcherRules.cs") (Expand-Template $matcherTests $map)
Write-Utf8File (Join-Path $testDir "BusinessRules\${pascal}PageMetaDataExtractorRules.cs") (Expand-Template $extractorTests $map)

Write-Host ""
Write-Host "Scaffolded $libName and $testName."
Write-Host ""
Write-Host "Remaining checklist (add-streaming-service skill):"
Write-Host "  1. StreamingServiceKeys.$pascal = `"$Key`""
Write-Host "  2. NonPodcastService.$pascal enum value"
Write-Host "  3. KnownStreamingServices.All += ${pascal}StreamingService.Registration"
Write-Host "  4. StreamingCatalog.csproj ProjectReference to $libName"
Write-Host "  5. AddNonPodcastScrapers().Add${pascal}Services()"
Write-Host "  6. RedditPodcastPoster.slnx — add both projects"
Write-Host "  7. Hand-tune Matching/${pascal}UrlMatcher.cs path grammar"
Write-Host "  8. Hand-tune ShowName / film rules in Extractors"
Write-Host "  9. StreamingScraperCanonicalCases + StreamingScraperProvider.$pascal"
Write-Host " 10. Api streaming-submit-contract.ts/.json + version bump"
Write-Host " 11. Website service-catalog + podcast-url-matcher + icon $Icon + contract copy"
Write-Host " 12. pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged"
Write-Host " 13. Sibling assert-streaming-submit-contract-copy.ps1 in Api consumers"
Write-Host ""
Write-Host "Skill: .cursor/skills/add-streaming-service/SKILL.md"
