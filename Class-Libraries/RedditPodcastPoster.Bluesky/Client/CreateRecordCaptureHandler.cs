using System.Net.Http;
using System.Text;

namespace RedditPodcastPoster.Bluesky.Client;

internal sealed class CreateRecordEditState
{
    public IReadOnlyList<MentionSpan> Mentions { get; set; } = [];
    public Dictionary<string, string> DidByHandle { get; } = new(StringComparer.Ordinal);
    public string? CreatedUri { get; set; }

    public void Reset()
    {
        Mentions = [];
        DidByHandle.Clear();
        CreatedUri = null;
    }
}

internal sealed class CreateRecordCaptureHandler : DelegatingHandler
{
    private readonly CreateRecordEditState _state;

    public CreateRecordCaptureHandler(CreateRecordEditState state)
    {
        _state = state;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (IsCreateRecord(request) && request.Content != null)
        {
            var json = await request.Content.ReadAsStringAsync(cancellationToken);
            var restored = CreateRecordMentionPatch.Restore(json, _state.Mentions, _state.DidByHandle);
            request.Content = new StringContent(restored, Encoding.UTF8, "application/json");
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (!IsCreateRecord(request) || response.Content == null || !response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
        response.Content = new StringContent(body, Encoding.UTF8, mediaType);
        _state.CreatedUri = CreateRecordMentionPatch.ReadCreatedUri(body);
        return response;
    }

    private static bool IsCreateRecord(HttpRequestMessage request)
    {
        return request.Method == HttpMethod.Post
               && request.RequestUri?.AbsolutePath.EndsWith(
                   "/xrpc/com.atproto.repo.createRecord",
                   StringComparison.Ordinal) == true;
    }
}
