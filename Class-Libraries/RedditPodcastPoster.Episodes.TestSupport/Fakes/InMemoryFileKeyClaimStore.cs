using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

/// <summary>
/// Process-local file-key claims for tests. Same results as <c>FileKeyClaimStore</c>: create, 409 for another
/// owner, a second scope that only observes an uncommitted claim does not release it, a retry takes over the
/// lease when no scope in this process is tracking it, acknowledge keeps the claim, and releasing the scope
/// drops one that was never saved only while that scope still holds the lease.
/// </summary>
public sealed class InMemoryFileKeyClaimStore : IFileKeyClaimStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Claim> _docs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Dictionary<string, Guid>> _scopes = [];

    public Task<FileKeyClaimResult> TryClaim(
        string fileKey,
        Guid ownerId,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var id = FileKeyClaim.DocumentIdFor(fileKey);
            if (_docs.TryGetValue(id, out var existing))
            {
                if (existing.OwnerId != ownerId)
                {
                    return Task.FromResult(FileKeyClaimResult.HeldByOther);
                }

                if (existing.Committed || IsTracked(fileKey))
                {
                    return Task.FromResult(FileKeyClaimResult.AlreadyOurs);
                }

                _docs[id] = existing with { ScopeId = scopeId };
                Track(scopeId, fileKey, ownerId);
                return Task.FromResult(FileKeyClaimResult.AlreadyOurs);
            }

            _docs[id] = new Claim(ownerId, scopeId, false);
            Track(scopeId, fileKey, ownerId);
            return Task.FromResult(FileKeyClaimResult.Created);
        }
    }

    public Task Acknowledge(string? fileKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            if (!Untrack(fileKey, out _))
            {
                return Task.CompletedTask;
            }

            var id = FileKeyClaim.DocumentIdFor(fileKey);
            if (_docs.TryGetValue(id, out var claim))
            {
                _docs[id] = claim with { Committed = true };
            }
        }

        return Task.CompletedTask;
    }

    public Task ReleaseUnacknowledged(Guid scopeId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_scopes.Remove(scopeId, out var pending))
            {
                return Task.CompletedTask;
            }

            foreach (var (fileKey, _) in pending)
            {
                var id = FileKeyClaim.DocumentIdFor(fileKey);
                if (_docs.TryGetValue(id, out var claim) && !claim.Committed && claim.ScopeId == scopeId)
                {
                    _docs.Remove(id);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task Delete(string fileKey, Guid ownerId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Untrack(fileKey, out _);
            var id = FileKeyClaim.DocumentIdFor(fileKey);
            if (_docs.TryGetValue(id, out var claim) && claim.OwnerId == ownerId)
            {
                _docs.Remove(id);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsTracked(string fileKey)
    {
        foreach (var pending in _scopes.Values)
        {
            if (pending.ContainsKey(fileKey))
            {
                return true;
            }
        }

        return false;
    }

    private void Track(Guid scopeId, string fileKey, Guid ownerId)
    {
        if (!_scopes.TryGetValue(scopeId, out var pending))
        {
            pending = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            _scopes[scopeId] = pending;
        }

        pending[fileKey] = ownerId;
    }

    private bool Untrack(string fileKey, out Guid ownerId)
    {
        ownerId = Guid.Empty;
        var found = false;
        foreach (var pending in _scopes.Values)
        {
            if (pending.Remove(fileKey, out var owner))
            {
                ownerId = owner;
                found = true;
            }
        }

        return found;
    }

    private readonly record struct Claim(Guid OwnerId, Guid ScopeId, bool Committed);
}
