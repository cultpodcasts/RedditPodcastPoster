//using Microsoft.AspNetCore.Authorization;
//using Microsoft.Extensions.Logging;

//namespace Api.Auth;

//public class HasScopeHandler : AuthorizationHandler<HasScopeRequirement>
//{
//    private readonly ILogger _logger;

//    public HasScopeHandler(ILoggerFactory loggerFactory)
//    {
//        _logger = loggerFactory.CreateLogger(GetType().FullName ?? nameof(HasScopeHandler));
//    }

//    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasScopeRequirement requirement)
//    {
//        if (!context.User.HasClaim(c => c.Type == "scope" && c.Issuer == requirement.Issuer))
//        {
//            var claims = context.User.Claims.Select(x => $"(type: '{x.Type}', issuer: '{x.Issuer}')");
//            _logger.LogWarning(
//                $"{nameof(HandleRequirementAsync)}: No claim of type 'scope' and no issuer matching '{requirement.Issuer}'. Claims: <{string.Join(",", claims)}>, identity-name: '{context.User.Identity?.Name}', auth-type: '{context.User.Identity?.AuthenticationType}'.");
//            return Task.CompletedTask;
//        }

//        // Split the scopes string into an array
//        var scopes = context.User.FindFirst(c => c.Type == "scope" && c.Issuer == requirement.Issuer)!.Value.Split(' ');

//        // Succeed if the scope array contains the required scope
//        if (scopes.Any(s => s == requirement.Scope))
//        {
//            context.Succeed(requirement);
//            _logger.LogInformation($"{nameof(HandleRequirementAsync)}: Found claim of scope '{requirement.Scope}'.");
//        }
//        else
//        {
//            _logger.LogWarning($"{nameof(HandleRequirementAsync)}: No claim found of scope '{requirement.Scope}'.");
//        }

//        return Task.CompletedTask;
//    }
//}