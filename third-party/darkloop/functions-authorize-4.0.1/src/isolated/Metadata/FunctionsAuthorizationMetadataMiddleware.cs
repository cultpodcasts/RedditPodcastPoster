// <copyright file="FunctionsAuthorizationMetadataMiddleware.cs" company="DarkLoop" author="Arturo Martinez">
//  Copyright (c) DarkLoop. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DarkLoop.Azure.Functions.Authorization.Extensions;
using DarkLoop.Azure.Functions.Authorization.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DarkLoop.Azure.Functions.Authorization.Metadata;

/// <summary>
///     Classifies functions based on their extension type.
/// </summary>
internal sealed class FunctionsAuthorizationMetadataMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<FunctionsAuthorizationMetadataMiddleware> _logger;
    private readonly FunctionsAuthorizationOptions _options;
    private readonly ConcurrentDictionary<string, bool> _trackedHttp = new();

    public FunctionsAuthorizationMetadataMiddleware(
        IOptions<FunctionsAuthorizationOptions> options,
        ILogger<FunctionsAuthorizationMetadataMiddleware> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        _logger.LogInformation($"{nameof(Invoke)} initiated.");
        if (!_trackedHttp.GetOrAdd(context.FunctionId, static (_, c) => c.IsHttpTrigger(), context))
        {
            _logger.LogInformation($"{nameof(Invoke)} Not Http-Trigger.");
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(Invoke)} (1) exception.");
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, $"{nameof(Invoke)} (1) inner-exception.");
                }

                _logger.LogError(ex, $"{nameof(Invoke)} exception.");
                throw;
            }

            return;
        }

        _logger.LogInformation($"{nameof(Invoke)} Is Http-Trigger.");
        if (!_options.IsFunctionRegistered(context.FunctionDefinition.Name))
        {
            _logger.LogInformation($"{nameof(Invoke)} Function not registered. Registering");
            RegisterHttpTriggerAuthorization(context);
        }
        else
        {
            _logger.LogInformation($"{nameof(Invoke)} Function registered.");
        }

        context.Features.Set<IFunctionsAuthorizationFeature>(
            new FunctionsAuthorizationFeature(context.FunctionDefinition.Name));

        try
        {
            _logger.LogInformation($"{nameof(Invoke)} Invoking next Name: '{context.FunctionDefinition.Name}', Entry-Point '{context.FunctionDefinition.EntryPoint}'.");
            await next(context);
            _logger.LogInformation($"{nameof(Invoke)} Invoked complete Name: '{context.FunctionDefinition.Name}' Entry-Point '{context.FunctionDefinition.EntryPoint}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(Invoke)} exception.");
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, $"{nameof(Invoke)} (2) inner-exception.");
            }

            _logger.LogError(ex, $"{nameof(Invoke)} (2) exception.");
            throw;
        }
    }

    private void RegisterHttpTriggerAuthorization(FunctionContext context)
    {
        var functionName = context.FunctionDefinition.Name;
        var declaringTypeName = context.FunctionDefinition.EntryPoint.LastIndexOf('.') switch
        {
            -1 => string.Empty,
            var index => context.FunctionDefinition.EntryPoint[..index]
        };

        var methodName = context.FunctionDefinition.EntryPoint[(declaringTypeName.Length + 1)..];
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var method = assemblies.Select(a => a.GetType(declaringTypeName, false))
                         .FirstOrDefault(t => t is not null)?
                         .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static) ??
                     throw new MethodAccessException(
                         $"Method instance for function '{context.FunctionDefinition.Name}' " +
                         $"cannot be found or cannot be accessed due to its protection level.");

        var declaringType = method.DeclaringType!;

        _options.RegisterFunctionAuthorizationAttributesMetadata<AuthorizeAttribute>(functionName, declaringType,
            method);
        _logger.LogInformation($"{nameof(RegisterHttpTriggerAuthorization)} Function-name '{functionName}', Declaring-Type: '{declaringType}', Method: '{method}'.");
    }
}