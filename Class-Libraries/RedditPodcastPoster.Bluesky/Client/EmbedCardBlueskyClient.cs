using System.Collections.Frozen;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using X.Bluesky;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Client;

public class EmbedCardBlueskyClient : IEmbedCardBlueskyClient
{
    // X.Bluesky's FacetBuilder mention-regex (@\w+(\.\w+)*) does not permit dashes, so handles such as
    // @morningjoe-msnow.bsky.social are truncated and then fail DID resolution. This regex follows the
    // atproto handle spec: dot-separated segments of alphanumerics/dashes that must not start/end with a dash.
    private static readonly Regex MentionRegex = new(
        @"(?<![\w@.-])@[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)+",
        RegexOptions.Compiled);

    private readonly IAuthorizationClient _authorizationClient;
    private readonly BlueskyClient _blueskyClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyCollection<string> _languages;
    private readonly ILogger _logger;
    private readonly IMentionResolver _mentionResolver;

    public EmbedCardBlueskyClient(
        IHttpClientFactory httpClientFactory,
        string identifier,
        string password,
        IEnumerable<string> languages,
        bool reuseSession,
        ILogger<EmbedCardBlueskyClient> logger,
        ILogger<BlueskyClient> blueskyClientLogger,
        ILogger<MentionResolver> mentionResolver
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        var uri = new Uri("https://bsky.social");
        _languages = languages.ToFrozenSet();
        _blueskyClient = new BlueskyClient(httpClientFactory, identifier, password, languages, reuseSession,
            blueskyClientLogger);
        _mentionResolver = new MentionResolver(_httpClientFactory, uri, mentionResolver);
        _authorizationClient = new AuthorizationClient(httpClientFactory, identifier, password, reuseSession, uri);
    }

    public Task Post(string text, EmbedCardRequest embedCard)
    {
        return Post(text, embedCard, _languages);
    }

    public Task Post(string text, EmbedCardRequest embedCard, string language)
    {
        return Post(text, embedCard, [language]);
    }

    private async Task Post(string text, EmbedCardRequest embedCard, IEnumerable<string> languages)
    {
        var session = await _authorizationClient.GetSession();

        if (session == null)
        {
            throw new AuthenticationException();
        }

        var (_, post) = await CreatePostAndFacets(text, languages);

        var embedCardBuilder = new EmbedCardBuilder(_httpClientFactory, session, _logger);

        post.Embed = new EmbedExternal
        {
            External = await embedCardBuilder.GetEmbedCard(embedCard)
        };

        await Post(session, post);
    }

    public Task Post(string text)
    {
        return Post(text, _languages);
    }

    public Task Post(string text, string language)
    {
        return Post(text, [language]);
    }

    private async Task Post(string text, IEnumerable<string> languages)
    {
        var session = await _authorizationClient.GetSession();

        if (session == null)
        {
            throw new AuthenticationException();
        }

        var (_, post) = await CreatePostAndFacets(text, languages);

        await Post(session, post);
    }

    public Task Post(string text, Uri uri)
    {
        return _blueskyClient.Post(text, uri);
    }

    public Task Post(string text, Image image)
    {
        return _blueskyClient.Post(text, image);
    }

    public Task Post(string text, Uri? url, Image image)
    {
        return _blueskyClient.Post(text, url, image);
    }

    public Task Post(string text, Uri? url, IEnumerable<Image> images)
    {
        return _blueskyClient.Post(text, url, images);
    }

    private async Task<(IReadOnlyCollection<Facet> facets, Post post)> CreatePostAndFacets(
        string text,
        IEnumerable<string> languages)
    {
        // Fetch the current time in ISO 8601 format, with "Z" to denote UTC
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var facetBuilder = new FacetBuilder();

        var facets = new List<Facet>();

        foreach (var match in facetBuilder.GetFeatureLinkMatches(text))
        {
            facets.Add(facetBuilder.CreateFacet(
                GetUtf8BytePosition(text, match.Index),
                GetUtf8BytePosition(text, match.Index + match.Length),
                new FacetFeatureLink {Uri = new Uri(match.Value)}));
        }

        foreach (var match in facetBuilder.GetFeatureTagMatches(text))
        {
            facets.Add(facetBuilder.CreateFacet(
                GetUtf8BytePosition(text, match.Index),
                GetUtf8BytePosition(text, match.Index + match.Length),
                new FacetFeatureTag {Tag = match.Value.Replace("#", string.Empty)}));
        }

        foreach (Match match in MentionRegex.Matches(text))
        {
            var resolvedDid = await _mentionResolver.ResolveMention(match.Value);

            if (string.IsNullOrWhiteSpace(resolvedDid))
            {
                _logger.LogWarning(
                    "Unable to resolve bluesky-mention '{mention}' to a DID. Posting without a mention-facet for it.",
                    match.Value);
                continue;
            }

            facets.Add(facetBuilder.CreateFacet(
                GetUtf8BytePosition(text, match.Index),
                GetUtf8BytePosition(text, match.Index + match.Length),
                new FacetFeatureMention {Did = resolvedDid}));
        }

        // Required fields for the post
        var post = new Post
        {
            Type = "app.bsky.feed.post",
            Text = text,
            CreatedAt = now,
            Langs = languages.ToList(),
            Facets = facets
        };
        return (facets, post);
    }

    private static int GetUtf8BytePosition(string text, int index)
    {
        return Encoding.UTF8.GetByteCount(text[..index]);
    }

    private async Task Post(Session session, Post post)
    {
        var requestUri = "https://bsky.social/xrpc/com.atproto.repo.createRecord";

        var requestData = new CreatePostRequest
        {
            Repo = session.Did,
            Collection = "app.bsky.feed.post",
            Record = post
        };

        var jsonRequest = JsonConvert.SerializeObject(requestData, Formatting.Indented, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        });

        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient();

        // Add the Authorization header with the bearer token
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

        var response = await httpClient.PostAsync(requestUri, content);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogError("Error: {ResponseContent}", responseContent);
        }

        // This throws an exception if the HTTP response status is an error code.
        response.EnsureSuccessStatusCode();
    }
}