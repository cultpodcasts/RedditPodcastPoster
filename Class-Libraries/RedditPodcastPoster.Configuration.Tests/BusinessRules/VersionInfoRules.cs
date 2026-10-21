using System.Reflection;
using System.Reflection.Emit;
using AutoFixture;
using FluentAssertions;

namespace RedditPodcastPoster.Configuration.Tests.BusinessRules;

[CollectionDefinition(Name, DisableParallelization = true)]
public class ConsoleStreamsCollection
{
    public const string Name = "Tests that replace Console.Out and Console.Error";
}

[Collection(ConsoleStreamsCollection.Name)]
public class VersionInfoRules : IDisposable
{
    private readonly Fixture _fixture = new();
    private readonly StringWriter _output = new();
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }

    [Fact(DisplayName =
        "A --version request prints the assembly name and its informational version once and asks the caller to exit, because the block is printed only by this one handler.")]
    public void Version_request_prints_the_block_once()
    {
        // Arrange
        var informationalVersion = $"{_fixture.Create<Version>()}{Environment.NewLine}{_fixture.Create<string>()}";
        var assembly = CreateAssembly(informationalVersion);

        // Act
        var handled = VersionInfo.TryHandleVersionRequest([VersionInfo.VersionArgument], _output, assembly);

        // Assert
        handled.Should().BeTrue();
        _output.ToString().Should().Be($"{assembly.GetName().Name} {informationalVersion}{Environment.NewLine}");
    }

    [Fact(DisplayName =
        "A --version request among verb arguments is still answered, because verb-based console apps take the flag after the verb.")]
    public void Version_request_after_a_verb_is_answered()
    {
        // Arrange
        var args = new[] { _fixture.Create<string>(), VersionInfo.VersionArgument };

        // Act
        var handled = VersionInfo.TryHandleVersionRequest(args, _output, CreateAssembly());

        // Assert
        handled.Should().BeTrue();
        _output.ToString().Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "Arguments without --version print nothing and let the caller carry on, because only an explicit request answers with the version.")]
    public void Other_arguments_print_nothing()
    {
        // Arrange
        var args = _fixture.CreateMany<string>().ToArray();

        // Act
        var handled = VersionInfo.TryHandleVersionRequest(args, _output, CreateAssembly());

        // Assert
        handled.Should().BeFalse();
        _output.ToString().Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An assembly without an informational version prints its own name and assembly version, because the block must still identify the build.")]
    public void Assembly_without_informational_version_prints_its_assembly_version()
    {
        // Arrange
        var version = CreateAssemblyVersion();
        var assembly = CreateAssembly(version: version);

        // Act
        var handled = VersionInfo.TryHandleVersionRequest([VersionInfo.VersionArgument], _output, assembly);

        // Assert
        handled.Should().BeTrue();
        _output.ToString().Should().Be($"{assembly.GetName().Name} {version}{Environment.NewLine}");
    }

    [Fact(DisplayName =
        "With no entry assembly the version request prints the default version, because the block must still print.")]
    public void No_assembly_prints_the_default_version()
    {
        // Arrange
        var args = new[] { VersionInfo.VersionArgument };

        // Act
        var handled = VersionInfo.TryHandleVersionRequest(args, _output, null);

        // Assert
        handled.Should().BeTrue();
        _output.ToString().Should().Be($" {VersionInfo.DefaultVersion}{Environment.NewLine}");
    }

    [Fact(DisplayName =
        "The entry-point overload prints the entry assembly's version block once to standard output and nothing to standard error, because that is the overload every console app calls.")]
    public void Entry_point_overload_prints_once_to_standard_output_only()
    {
        // Arrange
        var standardError = new StringWriter();
        Console.SetOut(_output);
        Console.SetError(standardError);
        var entryAssemblyName = Assembly.GetEntryAssembly()!.GetName().Name!;

        // Act
        var handled = VersionInfo.TryHandleVersionRequest([VersionInfo.VersionArgument]);

        // Assert
        handled.Should().BeTrue();
        var printed = _output.ToString();
        printed.Should().StartWith($"{entryAssemblyName} ");
        printed.Split(entryAssemblyName).Length.Should().Be(2, "the block is printed once");
        standardError.ToString().Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The entry-point overload prints nothing to either stream when --version is absent, because the app must carry on with its work.")]
    public void Entry_point_overload_prints_nothing_without_the_version_argument()
    {
        // Arrange
        var standardError = new StringWriter();
        Console.SetOut(_output);
        Console.SetError(standardError);

        // Act
        var handled = VersionInfo.TryHandleVersionRequest(_fixture.CreateMany<string>().ToArray());

        // Assert
        handled.Should().BeFalse();
        _output.ToString().Should().BeEmpty();
        standardError.ToString().Should().BeEmpty();
    }

    // Assembly version parts are 16-bit; bytes keep generated versions representable.
    private Version CreateAssemblyVersion() =>
        new(_fixture.Create<byte>(), _fixture.Create<byte>(), _fixture.Create<byte>(), _fixture.Create<byte>());

    private AssemblyBuilder CreateAssembly(string? informationalVersion = null, Version? version = null)
    {
        var name = new AssemblyName($"a{_fixture.Create<Guid>():N}") { Version = version ?? CreateAssemblyVersion() };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        if (informationalVersion != null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }

        return assembly;
    }
}

