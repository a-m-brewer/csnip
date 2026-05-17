# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

`csnip` is a CLI tool for managing reusable code/command snippets. It stores snippets as JSON and provides two commands:

- `csnip add [command] [--description|-d <desc>] [--tags|-t <tag>...]` — save a snippet (interactive prompts if args omitted)
- `csnip copy` — interactively select a snippet and copy it to the clipboard

Snippets support a template syntax: `<key>` or `<key=default>` placeholders that get filled in at copy time via Spectre prompts.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~HandleAsync_WithCommandOnly"

# Run tests in a specific fixture
dotnet test --filter "FullyQualifiedName~AddCommandHandlerTests"

# Run the tool locally
dotnet run --project src/CSnip -- add "git status" --description "Check repo" --tags git

# Publish native AOT binary
dotnet publish src/CSnip
```

## Architecture

The entry point (`Program.cs`) wires up `Microsoft.Extensions.Hosting`, registers services, then builds a `System.CommandLine` root command and routes invocations to handler classes.

**Handler pattern** — every CLI subcommand maps to an `ICliCommandHandler` and an inner `Symbols` class implementing `ICommandSymbols`. `CommandExtensions.SetActionHandler<THandler, TSymbols>` registers the symbols on the `Command` object and wires `SetAction` to resolve the handler from DI and call `HandleAsync`. Handlers are auto-registered by Scrutor scanning all `ICliCommandHandler` implementations.

**Service layer** (`Services/`) — `SnippetSelectionOrchestrator` coordinates the copy flow: it calls `ISnippetPromptService` to let the user pick a snippet, then uses `ITemplateService` to detect and fill placeholders, then `ITemplatePromptService` for per-variable prompts. All console interaction goes through `Spectre.Console`'s `IAnsiConsole`.

**Persistence** — `SnippetRepository` reads/writes a JSON file via `IFileSystem`. The path defaults to `%APPDATA%/csnip/snippets.json` and is configurable via `appsettings.json` under `Store:SnippetsPath`. JSON serialization uses a source-generated `SnippetJsonContext` (AOT compatible).

**AOT** — the main project targets `PublishAot=true`. Keep JSON serialization through `SnippetJsonContext.Default.*` and avoid reflection-based APIs.

## Testing approach

Unit tests live in `tests/CSnip.Tests/` and mirror the `src/` namespace structure. Tests prove correctness of each handler and service in isolation — use them to demonstrate that new behaviour works and existing behaviour is preserved.

**Test stack:**
- **NUnit 4** — test framework (`[TestFixture]`, `[Test]`, `[SetUp]`)
- **Moq + Moq.AutoMock** — `AutoMocker` auto-creates mocks for all constructor dependencies; call `_mocker.CreateInstance<T>()` to get the SUT
- **AwesomeAssertions** — fluent assertions (`.Should().Be(...)`, `.Should().BeEquivalentTo(...)`)
- **Spectre.Console.Testing** — `TestConsole` simulates interactive input (`console.Input.PushTextWithEnter(...)`) and captures output; inject via `_mocker.Use<IAnsiConsole>(console)`

**Typical test structure:**

```csharp
private AutoMocker _mocker = null!;

[SetUp]
public void SetUp() => _mocker = new AutoMocker();

[Test]
public async Task Handler_Scenario_ExpectedOutcome()
{
    // Arrange: configure mocks via _mocker.GetMock<IDep>().Setup(...)
    // Act:     var sut = _mocker.CreateInstance<SUT>(); await sut.HandleAsync(...)
    // Assert:  result.Should().Be(0); _mocker.GetMock<IDep>().Verify(...)
}
```

For handlers that need `ParseResult`, construct it by building a `Command`, adding the handler's `Symbols`, then calling `command.Parse(args)`.
