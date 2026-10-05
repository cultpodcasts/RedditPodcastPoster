namespace RedditPodcastPoster.TheTvdb.Clients;

/// <summary>
/// Process-wide TheTVDB login token. Typed clients stay transient and share this session,
/// so a new client does not log in again and does not keep the token on the instance.
/// </summary>
public sealed class TheTvdbLoginSession
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;

    public async Task<string> GetOrCreateAsync(
        Func<CancellationToken, Task<string>> login,
        CancellationToken cancellationToken)
    {
        var existing = Volatile.Read(ref _token);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            existing = Volatile.Read(ref _token);
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            var created = await login(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _token, created);
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => Volatile.Write(ref _token, null);
}
