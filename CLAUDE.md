# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

`csnip` is a CLI tool for managing reusable code/command snippets. It stores snippets as JSON and provides three commands:

- `csnip add [command] [--description|-d <desc>] [--tags|-t <tag>...]` — save a snippet (interactive prompts if args omitted)
- `csnip list [--format json|text|auto]` — list all snippets; pipe-friendly (auto-detects TTY vs redirected)
- `csnip copy` — interactively select a snippet and copy it to the clipboard; accepts piped NDJSON or bare command strings on stdin

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

## Testing interactive and piped features

Unit tests cover logic but not the full interactive TTY flow. For end-to-end testing of pipe scenarios use `expect`, which drives a pseudo-terminal so the interactive prompts receive real keystrokes:

```bash
# Single snippet via text fallback — no interaction needed
expect -c '
  spawn sh -c "echo \"git status\" | dotnet run --project src/CSnip -- copy 2>&1"
  expect "Copied"
'

# Multi-snippet: pipe list output, navigate with arrow keys, select with Enter
expect -c '
  spawn sh -c "dotnet run --project src/CSnip -- list --format json | dotnet run --project src/CSnip -- copy 2>&1"
  expect "Type to search"
  send "\r"          ;# Enter selects first item
  expect "Copied"
'

# With down-arrow navigation
expect -c '
  spawn sh -c "dotnet run --project src/CSnip -- list --format json | dotnet run --project src/CSnip -- copy 2>&1"
  expect "Type to search"
  send "\033\[B"     ;# Down arrow
  after 300
  send "\r"
  expect { "user" { send "\r"; exp_continue } "Copied" {} }
'

# Grep filter (should reduce to one match, skipping the picker)
expect -c '
  spawn sh -c "dotnet run --project src/CSnip -- list --format json | grep docker | dotnet run --project src/CSnip -- copy 2>&1"
  expect "Copied"
'
```

Key bindings sent by `expect`:
- `\r` — Enter (confirm / select)
- `\033\[A` — Up arrow
- `\033\[B` — Down arrow
- `\033\[C` / `\033\[D` — Right / Left arrow
- `\x1b` — Escape (cancel prompt)

## Architecture

The entry point (`Program.cs`) wires up `Microsoft.Extensions.Hosting`, registers services, then builds a `System.CommandLine` root command and routes invocations to handler classes.

**Handler pattern** — every CLI subcommand maps to an `ICliCommandHandler` and an inner `Symbols` class implementing `ICommandSymbols`. `CommandExtensions.SetActionHandler<THandler, TSymbols>` registers the symbols on the `Command` object and wires `SetAction` to resolve the handler from DI and call `HandleAsync`. Handlers are auto-registered by Scrutor scanning all `ICliCommandHandler` implementations.

**Service layer** (`Services/`) — `SnippetSelectionOrchestrator` coordinates the copy flow: it calls `ISnippetPromptService` to let the user pick a snippet, then uses `ITemplateService` to detect and fill placeholders, then `ITemplatePromptService` for per-variable prompts. All console interaction goes through `Spectre.Console`'s `IAnsiConsole`.

**Persistence** — `SnippetRepository` reads/writes a JSON file via `IFileSystem`. The path defaults to `%APPDATA%/csnip/snippets.json` and is configurable via `appsettings.json` under `Store:SnippetsPath`. JSON serialization uses a source-generated `SnippetJsonContext` (AOT compatible).

**Piping** — `list` writes NDJSON (one snippet per line) when stdout is redirected; `copy` reads NDJSON or bare command strings from stdin when redirected. `IConsoleEnvironment` carries `IsInputRedirected`, `IsOutputRedirected`, and the captured stdin `TextReader`. When stdin is piped, `Program.cs` drains it into a `StringReader` at startup; `TtyAnsiConsole` / `TtyConsoleInput` then open `/dev/tty` on a separate file descriptor (using P/Invoke `tcgetattr`/`tcsetattr`/`read`) to provide raw-mode keyboard input for Spectre prompts. This bypasses `Console.ReadKey`, which on .NET 10 checks the cached `Console.IsInputRedirected` flag and throws even after a dup2.

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
