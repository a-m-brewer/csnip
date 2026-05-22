using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Handlers;
using CSnip.Models;
using CSnip.Models.Settings;
using CSnip.Persistence;
using CSnip.Pipeline;
using CSnip.Services;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Spectre.Console;
using Spectre.Console.Testing;
using System.CommandLine;

namespace CSnip.Tests.Handlers;

[TestFixture]
public class SshCommandHandlerTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsInputRedirected).Returns(false);
        _mocker.Use<IOptions<SshSettings>>(Options.Create(new SshSettings()));
    }

    private static ParseResult BuildParseResult(params string[] args)
    {
        var command = new Command("ssh");
        ICommandSymbols symbols = new SshCommandHandler.Symbols();
        foreach (var arg in symbols.Arguments) command.Add(arg);
        foreach (var opt in symbols.Options) command.Add(opt);
        return command.Parse(args);
    }

    private void UseSettings(SshSettings settings) =>
        _mocker.Use<IOptions<SshSettings>>(Options.Create(settings));

    private void SetupAnyProcessExecution(int exitCode = 0) =>
        _mocker.GetMock<IShellExecutor>()
            .Setup(s => s.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exitCode);

    private void SetupProcessExecution(string fileName, int exitCode, params string[] arguments) =>
        _mocker.GetMock<IShellExecutor>()
            .Setup(s => s.ExecuteAsync(
                fileName,
                It.Is<IReadOnlyList<string>>(actual => actual.SequenceEqual(arguments)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exitCode);

    private void VerifyProcessExecution(string fileName, Times times, params string[] arguments) =>
        _mocker.GetMock<IShellExecutor>()
            .Verify(s => s.ExecuteAsync(
                fileName,
                It.Is<IReadOnlyList<string>>(actual => actual.SequenceEqual(arguments)),
                It.IsAny<CancellationToken>()), times);

    private void VerifyNoProcessExecution() =>
        _mocker.GetMock<IShellExecutor>()
            .Verify(s => s.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);

    private void SetupSnippetAndOrchestrator(string snippetCommand, string resolvedCommand)
    {
        IReadOnlyList<Snippet> snippets = [new Snippet(snippetCommand, null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedCommand);
        SetupAnyProcessExecution();
    }

    // ── non-redirected ────────────────────────────────────────────────────────

    [Test]
    public async Task HandleAsync_NoSnippets_DoesNotExecuteAndReturnsZero()
    {
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyNoProcessExecution();
    }

    [Test]
    public async Task HandleAsync_SingleHost_ExecutesSshCommandAndReturnsExitCode()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("git status");
        SetupProcessExecution("ssh", 0, "user@host1", "git status");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("user@host1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("ssh", Times.Once(), "user@host1", "git status");
    }

    [Test]
    public async Task HandleAsync_MultipleHosts_ExecutesOnEachHost()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("uptime", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("uptime");
        SetupAnyProcessExecution();

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1", "host2"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("ssh", Times.Once(), "host1", "uptime");
        VerifyProcessExecution("ssh", Times.Once(), "host2", "uptime");
    }

    [Test]
    public async Task HandleAsync_WithExtraSshArgs_IncludesThemBeforeHost()
    {
        // Hosts come first; SSH flags follow — tokens before the first '-'-prefixed one are hosts.
        IReadOnlyList<Snippet> snippets = [new Snippet("uptime", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("uptime");
        SetupAnyProcessExecution();

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        // host1 -o BatchMode=yes → host1 is the host, -o BatchMode=yes are SSH args
        var result = await sut.HandleAsync(BuildParseResult("host1", "-o", "BatchMode=yes"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("ssh", Times.Once(), "-o", "BatchMode=yes", "host1", "uptime");
    }

    // ── headers ───────────────────────────────────────────────────────────────

    [Test]
    public async Task HandleAsync_SingleHost_DoesNotPrintHeader()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);
        IReadOnlyList<Snippet> snippets = [new Snippet("uptime", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("uptime");
        SetupAnyProcessExecution();

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        console.Output.Should().NotContain("host1");
    }

    [Test]
    public async Task HandleAsync_MultipleHosts_PrintsHeaderPerHost()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);
        IReadOnlyList<Snippet> snippets = [new Snippet("uptime", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("uptime");
        SetupAnyProcessExecution();

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        await sut.HandleAsync(BuildParseResult("host1", "host2"), CancellationToken.None);

        console.Output.Should().Contain("host1");
        console.Output.Should().Contain("host2");
    }

    [Test]
    public async Task HandleAsync_MultipleHosts_WithNoHeader_SuppressesHeaders()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);
        IReadOnlyList<Snippet> snippets = [new Snippet("uptime", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("uptime");
        SetupAnyProcessExecution();

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        await sut.HandleAsync(BuildParseResult("host1", "host2", "--no-header"), CancellationToken.None);

        console.Output.Should().NotContain("host1");
        console.Output.Should().NotContain("host2");
    }

    [Test]
    public async Task HandleAsync_AllTokensAreSshArgs_NoHostsFound_ReturnsOneWithoutExecuting()
    {
        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("-o", "BatchMode=yes"), CancellationToken.None);

        result.Should().Be(1);
        VerifyNoProcessExecution();
    }

    [Test]
    public async Task HandleAsync_ShellReturnsNonZero_PrintsSummaryAndPropagatesExitCode()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);
        IReadOnlyList<Snippet> snippets = [new Snippet("false", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("false");
        SetupAnyProcessExecution(1);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(1);
        console.Output.Should().Contain("host1");
    }

    [Test]
    public async Task HandleAsync_MultipleHosts_OneFailure_ContinuesAndSummarizesAtEnd()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);
        IReadOnlyList<Snippet> snippets = [new Snippet("uptime", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("uptime");
        SetupProcessExecution("ssh", 255, "host1", "uptime");
        SetupProcessExecution("ssh", 0, "host2", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1", "host2"), CancellationToken.None);

        result.Should().Be(255);
        // host2 still ran despite host1 failing
        VerifyProcessExecution("ssh", Times.Once(), "host2", "uptime");
        // failure summary mentions the failed host
        console.Output.Should().Contain("host1");
        console.Output.Should().Contain("255");
    }

    [Test]
    public async Task HandleAsync_OrchestratorReturnsNull_DoesNotExecuteAndReturnsZero()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyNoProcessExecution();
    }

    // ── --ssh-program ─────────────────────────────────────────────────────────

    [Test]
    public async Task HandleAsync_WithSshProgram_UsesSpecifiedProgramForAllHosts()
    {
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("vm1", "vm2", "--ssh-program", "hvc ssh"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "vm1", "uptime");
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "vm2", "uptime");
    }

    [Test]
    public async Task HandleAsync_WithSshProgram_PerHostSshPrefixForcesPlainSsh()
    {
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("vm1", "ssh:jumpbox", "--ssh-program", "hvc ssh"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "vm1", "uptime");
        VerifyProcessExecution("ssh", Times.Once(), "jumpbox", "uptime");
    }

    // ── --profile ─────────────────────────────────────────────────────────────

    [Test]
    public async Task HandleAsync_WithProfile_UsesConfiguredProgram()
    {
        UseSettings(new SshSettings { Programs = new Dictionary<string, string> { ["hvc"] = "hvc ssh" } });
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("vm1", "--profile", "hvc"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "vm1", "uptime");
    }

    [Test]
    public async Task HandleAsync_WithProfile_PassesRemoteCommandWithSpacesAsSingleArgument()
    {
        UseSettings(new SshSettings { Programs = new Dictionary<string, string> { ["hvc"] = "hvc ssh" } });
        SetupSnippetAndOrchestrator("uname -r", "uname -r");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("root@anm-linux", "--profile", "hvc"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "root@anm-linux", "uname -r");
    }

    [Test]
    public async Task HandleAsync_WithUnknownProfile_ReturnsOneWithError()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("vm1", "--profile", "nonexistent"), CancellationToken.None);

        result.Should().Be(1);
        console.Output.Should().Contain("nonexistent");
        VerifyNoProcessExecution();
    }

    [Test]
    public async Task HandleAsync_SshProgramAndProfileBothSet_ReturnsOneWithError()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(
            BuildParseResult("vm1", "--ssh-program", "hvc ssh", "--profile", "hvc"),
            CancellationToken.None);

        result.Should().Be(1);
        console.Output.Should().Contain("mutually exclusive");
        VerifyNoProcessExecution();
    }

    // ── per-host prefix ───────────────────────────────────────────────────────

    [Test]
    public async Task HandleAsync_PerHostConfigAlias_UsesAliasProgram()
    {
        UseSettings(new SshSettings { Programs = new Dictionary<string, string> { ["hvc"] = "hvc ssh" } });
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("hvc:vm1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "vm1", "uptime");
    }

    [Test]
    public async Task HandleAsync_PerHostSshAlias_ForcesPlainSsh()
    {
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("ssh:user@host"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("ssh", Times.Once(), "user@host", "uptime");
    }

    [Test]
    public async Task HandleAsync_PerHostUnknownAlias_ReturnsOneWithError()
    {
        var console = new TestConsole();
        _mocker.Use<IAnsiConsole>(console);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("unknown:vm1"), CancellationToken.None);

        result.Should().Be(1);
        console.Output.Should().Contain("unknown");
        VerifyNoProcessExecution();
    }

    [Test]
    public async Task HandleAsync_MixedPrefixedAndPlainHosts_UsesCorrectProgramPerHost()
    {
        UseSettings(new SshSettings { Programs = new Dictionary<string, string> { ["hvc"] = "hvc ssh" } });
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("hvc:vm1", "user@server2", "ssh:jumpbox"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("hvc", Times.Once(), "ssh", "vm1", "uptime");
        VerifyProcessExecution("ssh", Times.Once(), "user@server2", "uptime");
        VerifyProcessExecution("ssh", Times.Once(), "jumpbox", "uptime");
    }

    [Test]
    public async Task HandleAsync_UserAtHostNotTreatedAsPrefix()
    {
        // "user@host" has no colon so it must never trigger alias resolution
        SetupSnippetAndOrchestrator("uptime", "uptime");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("user@host"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("ssh", Times.Once(), "user@host", "uptime");
    }

    // ── piped input ───────────────────────────────────────────────────────────

    private void SetupPipedInput(IReadOnlyList<Snippet> candidates)
    {
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsInputRedirected).Returns(true);
        _mocker.GetMock<IPipelineReader>()
            .Setup(r => r.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
    }

    [Test]
    public async Task HandleAsync_PipedEmpty_DoesNotExecuteAndReturnsZero()
    {
        SetupPipedInput([]);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyNoProcessExecution();
    }

    [Test]
    public async Task HandleAsync_PipedSingle_SkipsPickerAndExecutesOnHosts()
    {
        var snippet = new Snippet("git status", null, []);
        SetupPipedInput([snippet]);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveCommandAsync(snippet, It.IsAny<CancellationToken>()))
            .ReturnsAsync("git status");
        SetupProcessExecution("ssh", 0, "host1", "git status");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyProcessExecution("ssh", Times.Once(), "host1", "git status");
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Verify(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_PipedSingle_OrchestratorReturnsNull_DoesNotExecute()
    {
        var snippet = new Snippet("git status", null, []);
        SetupPipedInput([snippet]);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveCommandAsync(snippet, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(0);
        VerifyNoProcessExecution();
    }

    [Test]
    public async Task HandleAsync_PipedMany_ShowsPickerAndExecutes()
    {
        IReadOnlyList<Snippet> candidates =
        [
            new Snippet("git status", null, []),
            new Snippet("git log", null, []),
        ];
        SetupPipedInput(candidates);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(candidates, It.IsAny<CancellationToken>()))
            .ReturnsAsync("git status");
        SetupProcessExecution("ssh", 0, "host1", "git status");

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Verify(o => o.ResolveSnippetCommandAsync(candidates, It.IsAny<CancellationToken>()), Times.Once);
        VerifyProcessExecution("ssh", Times.Once(), "host1", "git status");
    }

    [Test]
    public async Task HandleAsync_NonRedirected_DoesNotReadFromPipelineReader()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<SshCommandHandler>();
        await sut.HandleAsync(BuildParseResult("host1"), CancellationToken.None);

        _mocker.GetMock<IPipelineReader>()
            .Verify(r => r.ReadAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
