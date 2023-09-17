using System.Net.Http.Headers;
using System.Security.Claims;

namespace Indexer.Auth0;

public interface ITokenValidator
{
    Task<ClaimsPrincipal> ValidateTokenAsync(AuthenticationHeaderValue value);
}