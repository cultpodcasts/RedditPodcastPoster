using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace RedditPodcastPoster.Auth0;

public record ValidatedToken(ClaimsPrincipal ClaimsPrincipal, SecurityToken Token);