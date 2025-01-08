using System.Collections.Frozen;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using X.Bluesky;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Client;

public class EmbedCardBlueskyClient : IEmbedCardBlueskyClient
{
    private readonly BlueskyClient _blueskyClient;
    private readonly IAuthorizationClient _authorizationClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyCollection<string> _languages;
    private readonly ILogger _logger;
    private readonly IMentionResolver _mentionResolver;

    /// <summary>
    ///     Creates a new instance of the Bluesky client
    /// </summary>
    /// <param name="httpClientFactory"></param>
    /// <param name="identifier">Bluesky identifier</param>
    /// <param name="password">Bluesky application password</param>
    /// <param name="languages">Post languages</param>
    /// <param name="reuseSession">Reuse session</param>
    /// <param name="logger"></param>
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
        _blueskyClient = new BlueskyClient(httpClientFactory, identifier, password, languages, reuseSession, blueskyClientLogger);
        _mentionResolver = new MentionResolver(_httpClientFactory, uri, mentionResolver);
        _authorizationClient = new AuthorizationClient(httpClientFactory, identifier, password, reuseSession, uri);
    }

    public async Task Post(string text, EmbedCardRequest embedCard)
    {
        var session = await _authorizationClient.GetSession();

        if (session == null)
        {
            throw new AuthenticationException();
        }

        var (facets, post) = await CreatePostAndFacets(text);

        var embedCardBuilder = new EmbedCardBuilder(_httpClientFactory, session, _logger);

        post.Embed = new EmbedExternal
        {
            External = await embedCardBuilder.GetEmbedCard(embedCard)
        };

        await Post(session, post);
    }

    public Task Post(string text)
    {
        return _blueskyClient.Post(text);
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

    private async Task<(IReadOnlyCollection<Facet> facets, Post post)> CreatePostAndFacets(string text)
    {
        // Fetch the current time in ISO 8601 format, with "Z" to denote UTC
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var facetBuilder = new FacetBuilder();

        var facets = facetBuilder.GetFacets(text);

        foreach (var facet in facets)
        {
            foreach (var facetFeature in facet.Features)
            {
                if (facetFeature is FacetFeatureMention facetFeatureMention)
                {
                    var resolveDid = await _mentionResolver.ResolveMention(facetFeatureMention.Did);

                    facetFeatureMention.ResolveDid(resolveDid);
                }
            }
        }

        // Required fields for the post
        var post = new Post
        {
            Type = "app.bsky.feed.post",
            Text = text,
            CreatedAt = now,
            Langs = _languages.ToList(),
            Facets = facets.ToList()
        };
        return (facets, post);
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