using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Support;

/// <summary>
/// Minimal IAPIConnector stub for paginator unit tests.
/// </summary>
internal sealed class FakeSpotifyApiConnector(IReadOnlyDictionary<string, object> pagesByUrl) : IAPIConnector
{
    public event EventHandler<IResponse>? ResponseReceived;

    Task<T> IAPIConnector.Get<T>(Uri uri, CancellationToken cancel)
    {
        if (pagesByUrl.TryGetValue(uri.ToString(), out var page) && page is T typed)
        {
            return Task.FromResult(typed);
        }

        throw new KeyNotFoundException($"No paginated response registered for '{uri}'.");
    }

    Task<T> IAPIConnector.Get<T>(Uri uri, IDictionary<string, string>? headers, CancellationToken cancel) =>
        ((IAPIConnector)this).Get<T>(uri, cancel);

    public Task<System.Net.HttpStatusCode> Get(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel = default) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Post<T>(Uri uri, CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Post<T>(Uri uri, IDictionary<string, string>? headers, CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Post<T>(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Post<T>(Uri uri, IDictionary<string, string>? headers, object? body,
        Dictionary<string, string>? queryParams, CancellationToken cancel) =>
        throw new NotImplementedException();

    public Task<System.Net.HttpStatusCode> Post(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel = default) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Put<T>(Uri uri, CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Put<T>(Uri uri, IDictionary<string, string>? headers, CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Put<T>(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel) =>
        throw new NotImplementedException();

    public Task<System.Net.HttpStatusCode> Put(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel = default) =>
        throw new NotImplementedException();

    public Task<System.Net.HttpStatusCode> PutRaw(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel = default) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Delete<T>(Uri uri, CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Delete<T>(Uri uri, IDictionary<string, string>? headers, CancellationToken cancel) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.Delete<T>(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel) =>
        throw new NotImplementedException();

    public Task<System.Net.HttpStatusCode> Delete(Uri uri, IDictionary<string, string>? headers, object? body,
        CancellationToken cancel = default) =>
        throw new NotImplementedException();

    Task<T> IAPIConnector.SendAPIRequest<T>(Uri uri, System.Net.Http.HttpMethod method,
        IDictionary<string, string>? headers, object? body, IDictionary<string, string>? queryParams,
        CancellationToken cancel) =>
        throw new NotImplementedException();

    public void SetRequestTimeout(TimeSpan timeout)
    {
    }
}
