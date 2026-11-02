using System.Collections;
using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Middleware;
using Xunit;

namespace FunctionHost.Tests.Api.Middleware;

/// <summary>
/// Rules for turning Isolated <c>[FromBody]</c> / input-converter failures into HTTP 400 with
/// <c>ApiErrorResponse</c>. Full host envelope still needs Core Tools or a deployed worker.
/// </summary>
public class FunctionInputBindingBadRequestMiddlewareRules
{
    private readonly AutoMocker _mocker = new();

    public FunctionInputBindingBadRequestMiddlewareRules()
    {
        _mocker.GetMock<ILogger<FunctionInputBindingBadRequestMiddleware>>();
    }

    [Fact(DisplayName =
        "A FunctionInputConverterException is an input-binding failure, so HTTP middleware may return 400 " +
        "instead of letting the host surface a 500.")]
    public void function_input_converter_exception_is_binding_failure()
    {
        // Arrange
        var exception = new FunctionInputConverterException(
            "Unable to convert input",
            new JsonException("The JSON value could not be converted to System.Nullable`1[System.Int32]."));

        // Act
        var isFailure = FunctionInputBindingFailure.IsInputBindingFailure(exception);

        // Assert
        isFailure.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A FunctionInputConverterException nested as InnerException is still an input-binding failure, " +
        "because the worker may wrap converter errors.")]
    public void nested_function_input_converter_exception_is_binding_failure()
    {
        // Arrange
        var exception = new InvalidOperationException(
            "wrapper",
            new FunctionInputConverterException("Unable to convert input"));

        // Act
        var isFailure = FunctionInputBindingFailure.IsInputBindingFailure(exception);

        // Assert
        isFailure.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A bare JsonException from application code is not treated as an input-binding failure, " +
        "so handler/service JSON faults still surface as errors rather than a client 400.")]
    public void bare_json_exception_is_not_binding_failure()
    {
        // Arrange
        var exception = new JsonException("handler serialisation fault");

        // Act
        var isFailure = FunctionInputBindingFailure.IsInputBindingFailure(exception);

        // Assert
        isFailure.Should().BeFalse();
    }

    [Fact(DisplayName =
        "CreateBadRequestAsync returns 400 with ApiErrorResponse.error explaining the body could not be read.")]
    public async Task create_bad_request_writes_api_error_response()
    {
        // Arrange
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PATCH");

        // Act
        var response = await FunctionInputBindingFailure.CreateBadRequestAsync(req.Object, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        document.RootElement.GetProperty("error").GetString()
            .Should().Be(FunctionInputBindingFailure.DefaultMessage);
    }

    [Fact(DisplayName =
        "When next throws FunctionInputConverterException on an HTTP invocation, middleware does not rethrow " +
        "and SetHttpInvocationResult receives a 400 ApiErrorResponse with the default bind-failure message, " +
        "so Isolated [FromBody] JSON failures surface as client errors instead of host 500s.")]
    public async Task http_binding_failure_sets_400_invocation_result()
    {
        // Arrange
        var sut = _mocker.CreateInstance<FunctionInputBindingBadRequestMiddleware>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PATCH");
        var (context, bindingsFeature) = CreateContextWithHttpRequest(req.Object);
        FunctionExecutionDelegate next = _ =>
            throw new FunctionInputConverterException(
                "Unable to convert input",
                new JsonException("The JSON value could not be converted to System.Nullable`1[System.Int32]."));

        // Act
        await sut.Invoke(context, next);

        // Assert
        var invocationResult = GetBindingsInvocationResult(bindingsFeature);
        var response = invocationResult.Should().BeAssignableTo<HttpResponseData>().Subject;
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        document.RootElement.GetProperty("error").GetString()
            .Should().Be(FunctionInputBindingFailure.DefaultMessage);
    }

    [Fact(DisplayName =
        "When next throws FunctionInputConverterException and the invocation is not HTTP, middleware rethrows, " +
        "because only HTTP triggers can return a 400 body.")]
    public async Task non_http_binding_failure_is_reththrown()
    {
        // Arrange
        var sut = _mocker.CreateInstance<FunctionInputBindingBadRequestMiddleware>();
        var context = CreateContextWithoutHttpRequest();
        FunctionExecutionDelegate next = _ =>
            throw new FunctionInputConverterException("Unable to convert input");

        // Act
        var act = () => sut.Invoke(context, next);

        // Assert
        await act.Should().ThrowAsync<FunctionInputConverterException>();
    }

    [Fact(DisplayName =
        "When next throws a non-binding exception, middleware does not swallow it.")]
    public async Task non_binding_exception_propagates()
    {
        // Arrange
        var sut = _mocker.CreateInstance<FunctionInputBindingBadRequestMiddleware>();
        var context = CreateContextWithoutHttpRequest();
        FunctionExecutionDelegate next = _ => throw new InvalidOperationException("unrelated");

        // Act
        var act = () => sut.Invoke(context, next);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("unrelated");
    }

    [Fact(DisplayName =
        "When next completes without throwing, middleware does not invent a response.")]
    public async Task successful_next_is_passthrough()
    {
        // Arrange
        var sut = _mocker.CreateInstance<FunctionInputBindingBadRequestMiddleware>();
        var context = CreateContextWithoutHttpRequest();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await sut.Invoke(context, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    private static FunctionContext CreateContextWithoutHttpRequest()
    {
        var mocker = new AutoMocker();
        var context = mocker.GetMock<FunctionContext>();
        var functionDefinition = mocker.GetMock<FunctionDefinition>();
        functionDefinition.SetupGet(d => d.Name).Returns("TestFunction");
        context.SetupGet(c => c.FunctionDefinition).Returns(functionDefinition.Object);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        // Explicit null request: DefaultHttpRequestDataFeature NREs without binding data.
        var httpRequestFeature = mocker.GetMock<IHttpRequestDataFeature>();
        httpRequestFeature
            .Setup(f => f.GetHttpRequestDataAsync(It.IsAny<FunctionContext>()))
            .ReturnsAsync((HttpRequestData?)null);

        var features = mocker.GetMock<IInvocationFeatures>();
        features.Setup(f => f.Get<IHttpRequestDataFeature>()).Returns(httpRequestFeature.Object);
        context.SetupGet(c => c.Features).Returns(features.Object);

        return context.Object;
    }

    /// <summary>
    /// HTTP context with a real request plus a stubbed worker bindings feature so
    /// <see cref="FunctionInputBindingFailure.SetHttpInvocationResult"/> can assign <c>$return</c>.
    /// <c>IFunctionBindingsFeature</c> is internal to the worker SDK — created via reflection.
    /// </summary>
    private static (FunctionContext Context, object BindingsFeature) CreateContextWithHttpRequest(
        HttpRequestData request)
    {
        var mocker = new AutoMocker();
        var context = mocker.GetMock<FunctionContext>();
        var functionDefinition = mocker.GetMock<FunctionDefinition>();
        functionDefinition.SetupGet(d => d.Name).Returns("TestFunction");
        functionDefinition.SetupGet(d => d.OutputBindings)
            .Returns(ImmutableDictionary<string, BindingMetadata>.Empty);
        context.SetupGet(c => c.FunctionDefinition).Returns(functionDefinition.Object);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var httpRequestFeature = mocker.GetMock<IHttpRequestDataFeature>();
        httpRequestFeature
            .Setup(f => f.GetHttpRequestDataAsync(It.IsAny<FunctionContext>()))
            .ReturnsAsync(request);

        var bindingsFeature = CreateBindingsFeatureStub();
        var features = new TestInvocationFeatures();
        features.Set(httpRequestFeature.Object);
        SetFeatureByReflection(features, bindingsFeature);

        context.SetupGet(c => c.Features).Returns(features);

        return (context.Object, bindingsFeature);
    }

    private static object CreateBindingsFeatureStub()
    {
        var bindingsFeatureType = typeof(FunctionContext).Assembly.GetType(
            "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature")
            ?? throw new InvalidOperationException("IFunctionBindingsFeature type not found.");

        var mockType = typeof(Mock<>).MakeGenericType(bindingsFeatureType);
        var mock = Activator.CreateInstance(mockType)!;
        mockType.GetMethod("SetupAllProperties")!.Invoke(mock, null);
        return mockType
            .GetProperty("Object", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!
            .GetValue(mock)!;
    }

    private static void SetFeatureByReflection(TestInvocationFeatures features, object bindingsFeature)
    {
        var bindingsFeatureType = bindingsFeature.GetType().GetInterfaces()
            .Single(i => i.Name == "IFunctionBindingsFeature");
        typeof(TestInvocationFeatures)
            .GetMethod(nameof(TestInvocationFeatures.Set))!
            .MakeGenericMethod(bindingsFeatureType)
            .Invoke(features, [bindingsFeature]);
    }

    private static object? GetBindingsInvocationResult(object bindingsFeature)
    {
        var property = bindingsFeature.GetType()
            .GetInterfaces()
            .Select(i => i.GetProperty("InvocationResult"))
            .FirstOrDefault(p => p is not null)
            ?? throw new InvalidOperationException("InvocationResult property not found.");
        return property.GetValue(bindingsFeature);
    }

    private sealed class TestInvocationFeatures : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> _features = new();

        public void Set<T>(T instance) => _features[typeof(T)] = instance!;

        public T? Get<T>() =>
            _features.TryGetValue(typeof(T), out var value) ? (T)value : default;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
