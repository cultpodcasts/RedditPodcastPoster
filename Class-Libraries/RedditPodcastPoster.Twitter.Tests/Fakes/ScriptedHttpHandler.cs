using System.Net;

namespace RedditPodcastPoster.Twitter.Tests.Fakes;

/// <summary>Records each request (with its Authorization header and body) and answers from a queue.</summary>
public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public ScriptedHttpHandler Respond(HttpStatusCode status, string? body = null,
        IDictionary<string, string>? headers = null)
    {
        _responses.Enqueue(() =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            }

            foreach (var header in headers ?? new Dictionary<string, string>())
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, request.Headers.Authorization, body));
        var response = _responses.Count > 0 ? _responses.Dequeue()() : new HttpResponseMessage(HttpStatusCode.OK);
        response.RequestMessage = request;
        return response;
    }
}

public sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    System.Net.Http.Headers.AuthenticationHeaderValue? Authorization,
    string? Body);
