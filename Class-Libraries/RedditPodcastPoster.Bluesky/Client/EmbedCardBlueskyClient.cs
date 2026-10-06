using System.Net.Http;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using X.Bluesky;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Client;

public class EmbedCardBlueskyClient : IEmbedCardBlueskyClient
{
    private static readonly Uri BlueskyBase = new("https://bsky.social");

    private readonly BlueskyClient _blueskyClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _logger;
    private readonly IMentionResolver _mentionResolver;
    private readonly CreateRecordEditState _state = new();

    public EmbedCardBlueskyClient(
        string identifier,
        string password,
        ILogger<EmbedCardBlueskyClient> logger,
        ILogger<BlueskyClient> blueskyClientLogger,
        ILogger<MentionResolver> mentionResolverLogger)
    {
        _logger = logger;
        var handler = new CreateRecordCaptureHandler(_state)
        {
            InnerHandler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            }
        };
        var httpClientFactory = new SharedBlueskyHttpClientFactory(handler);
        _blueskyClient = new BlueskyClient(identifier, password, BlueskyBase, httpClientFactory, blueskyClientLogger);
        _mentionResolver = new MentionResolver(httpClientFactory, BlueskyBase, mentionResolverLogger);
    }

    public async Task Post(Post post)
    {
        await PostReturningUri(post);
    }

    public Task<string> Post(string text, Uri url, string language)
    {
        return PostReturningUri(new Post
        {
            Text = text,
            Url = url,
            Languages = [language],
            GenerateCardForUrl = true
        });
    }

    public Task<string> Post(string text, string language)
    {
        return PostReturningUri(new Post
        {
            Text = text,
            Languages = [language],
            GenerateCardForUrl = true
        });
    }

    private async Task<string> PostReturningUri(Post post)
    {
        await _gate.WaitAsync();
        try
        {
            _state.Reset();
            var (maskedText, mentions) = CreateRecordMentionPatch.Mask(post.Text);
            _state.Mentions = mentions;
            foreach (var mention in mentions)
            {
                await RememberDid(mention.HandleWithAt);
            }

            await _blueskyClient.Post(post with { Text = maskedText });
            if (string.IsNullOrWhiteSpace(_state.CreatedUri))
            {
                throw new InvalidOperationException(
                    "Bluesky createRecord succeeded but response did not include an AT URI.");
            }

            return _state.CreatedUri;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RememberDid(string handleWithAt)
    {
        try
        {
            var did = await _mentionResolver.ResolveMention(handleWithAt);
            if (string.IsNullOrWhiteSpace(did))
            {
                _logger.LogWarning(
                    "Unable to resolve bluesky-mention '{mention}' to a DID. Posting without a mention-facet for it.",
                    handleWithAt);
                return;
            }

            _state.DidByHandle[handleWithAt] = did;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to resolve bluesky-mention '{mention}' to a DID. Posting without a mention-facet for it.",
                handleWithAt);
        }
    }

    private sealed class SharedBlueskyHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler, disposeHandler: false);

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
