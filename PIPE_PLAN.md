# Piping support for csnip

## Context

`csnip` is currently 100% interactive — every command reads from a TTY via `ILineReader` / `IAnsiConsole` and writes pretty output. We want to compose commands with pipes, e.g.

```
csnip list | csnip copy
csnip list | grep git | csnip copy
echo "git status" | csnip copy
```

…and have it work identically in POSIX shells and PowerShell (Windows + Unix).

**Key constraint:** when a non-PowerShell binary is invoked from PowerShell, the PS object pipeline does not apply — PS marshals to/from text on stdin/stdout. So a portable contract is just *bytes on the pipe*. We pick **NDJSON** (one JSON object per line) as the wire format: it is line-oriented (greps cleanly), structured (preserves description + tags), and trivially parsed by `ConvertFrom-Json` for PowerShell users who want objects.

**Decisions already made with the user:**
- NDJSON-only; no separate PowerShell module project.
- Format selection auto-detects (TTY → pretty text, redirected → NDJSON), overridable with `--format json|text`.
- First slice: add `csnip list` and make `csnip copy` pipe-aware. No `add` bulk import, no `filter` command yet.

## Wire format

**NDJSON output (one Snippet per line):**
```
{"Command":"git status","Description":"Check repo","Tags":["git"]}
{"Command":"ssh <user>@<host>","Description":"SSH in","Tags":["ssh","net"]}
```

**Text input fallback:** if piped input is not JSON, treat each non-empty line as a bare command string → `new Snippet(line, null, [])`. Lets `echo "git status" | csnip copy` work without ceremony.

**Format auto-detection:**
- Output: `Console.IsOutputRedirected` → JSON, else pretty text. `--format` overrides.
- Input: peek first non-whitespace byte from stdin; `{` or `[` → JSON, else text.

Invalid JSON lines on input → warn to stderr, skip line. Don't fail the whole pipe.

## New files

```
src/CSnip/
  Abstractions/
    ConsoleEnvironmentAbstraction.cs   IConsoleEnvironment { IsInputRedirected, IsOutputRedirected, In, Out }
  Pipeline/
    PipelineFormat.cs                  enum OutputFormat { Auto, Text, Json }
    IPipelineWriter.cs                 Task WriteAsync(IReadOnlyList<Snippet>, OutputFormat, CancellationToken)
    PipelineWriter.cs                  NDJSON writer (Console.Out) + Spectre Table for text
    IPipelineReader.cs                 Task<IReadOnlyList<Snippet>> ReadAllAsync(CancellationToken)
    PipelineReader.cs                  Sniff first byte, parse NDJSON or text lines
    FormatResolver.cs                  Resolve(requested, env) → effective OutputFormat
  Handlers/
    ListCommandHandler.cs              New handler + Symbols (only option: --format)
```

## Modified files

- **`src/CSnip/Persistence/SnippetJsonContext.cs`** — add `[JsonSerializable(typeof(Snippet))]` so we can (de)serialize a single record (AOT-safe). Keep `List<Snippet>` for repository.
- **`src/CSnip/Handlers/CopyCommandHandler.cs`** — if `_consoleEnv.IsInputRedirected`, read candidates from `IPipelineReader` instead of `_repository.GetAllAsync`. If the piped set has exactly one snippet, skip the picker and go straight to template resolution; otherwise hand the narrowed list to the existing orchestrator. Empty piped set → friendly message, exit 0.
- **`src/CSnip/Program.cs`** — register `IConsoleEnvironment`, `IPipelineReader`, `IPipelineWriter`, `FormatResolver` as singletons; add the `list` command via `SetActionHandler<ListCommandHandler, ListCommandHandler.Symbols>()`.

## Reuse (don't re-build)

- **`SnippetJsonContext`** at `src/CSnip/Persistence/SnippetJsonContext.cs:1` — extend, don't replace. AOT requires source-gen.
- **`ISnippetSelectionOrchestrator`** at `src/CSnip/Services/SnippetSelectionOrchestrator.cs:1` — `copy` keeps using this when there are 2+ candidates (piped or not). No need to fork the flow; just swap the candidate source.
- **`ITemplateService` + `ITemplatePromptService`** — the single-piped-snippet path still needs template resolution, which means calling `templateService.ParseTemplates` → `templatePromptService.PromptForTemplatesAsync` → `templateService.ApplyTemplates`. Worth adding a tiny method on the orchestrator (`ResolveCommandAsync(Snippet)`) so the handler doesn't duplicate that chain.
- **`CommandExtensions.SetActionHandler`** at `src/CSnip/Extensions/CommandExtensions.cs:9` — `list` registers exactly like `add` and `copy`. No change.
- **`IClipboardService`, `ISnippetRepository`, `IAnsiConsole`** — injected as today.

## Handler behavior

### `csnip list`
```
csnip list [--format json|text|auto]
```
- Load all snippets from `ISnippetRepository`.
- Resolve effective format via `FormatResolver` (flag wins, else TTY detection).
- Hand off to `IPipelineWriter.WriteAsync(snippets, format, ct)`.
  - Text: Spectre `Table` with Command / Description / Tags columns.
  - JSON: NDJSON line per snippet to `Console.Out` (no Spectre — must be machine-clean).

### `csnip copy` (post-change)
```
if (consoleEnv.IsInputRedirected) {
    candidates = await pipelineReader.ReadAllAsync(ct);
    if (candidates.Count == 0) { console.MarkupLine("[yellow]No snippets on stdin.[/]"); return 0; }
    if (candidates.Count == 1) {
        var command = await orchestrator.ResolveCommandAsync(candidates[0], ct); // new shortcut method
        if (command is null) return 0;
        clipboard.SetText(command);
        console.MarkupLine($"[green]Copied:[/] {Markup.Escape(command)}");
        return 0;
    }
} else {
    candidates = await repository.GetAllAsync(ct);
}
var resolved = await orchestrator.ResolveSnippetCommandAsync(candidates, ct);
// …existing copy logic
```

## Tests

Mirror existing structure under `tests/CSnip.Tests/`:
- `Pipeline/FormatResolverTests.cs` — auto+TTY=text, auto+redirected=json, explicit overrides.
- `Pipeline/PipelineWriterTests.cs` — NDJSON output for 0/1/many snippets, special chars escape correctly, text mode renders a table.
- `Pipeline/PipelineReaderTests.cs` — NDJSON happy path, text-line fallback, mixed whitespace, blank input, invalid JSON line skipped with stderr warning, sniffing `[` (array form) handled.
- `Handlers/ListCommandHandlerTests.cs` — text/json modes, empty repo.
- `Handlers/CopyCommandHandlerTests.cs` — extend existing fixture with: piped empty, piped single (orchestrator NOT called for picker), piped many (picker called over narrowed list), non-redirected (existing behavior unchanged).

All tests follow the AutoMocker pattern at `tests/CSnip.Tests/Handlers/AddCommandHandlerTests.cs:1`. Use `TestConsole` for `IAnsiConsole`. For `IConsoleEnvironment`, wrap an in-memory `StringReader`/`StringWriter` and set the `IsInputRedirected` flag explicitly per test.

## Verification

```bash
dotnet build
dotnet test

# Seed a couple of snippets first via interactive add, then:
dotnet run --project src/CSnip -- list                                         # pretty table
dotnet run --project src/CSnip -- list --format json                           # NDJSON
dotnet run --project src/CSnip -- list | dotnet run --project src/CSnip -- copy
dotnet run --project src/CSnip -- list | grep git | dotnet run --project src/CSnip -- copy
echo "git status" | dotnet run --project src/CSnip -- copy

# Cross-shell sanity (Unix pwsh or Windows pwsh):
pwsh -c "csnip list | csnip copy"
pwsh -c "csnip list | ConvertFrom-Json | Where-Object { \$_.Tags -contains 'git' } | ConvertTo-Json -Compress | csnip copy"
```

Watch for:
- AOT publish still works: `dotnet publish src/CSnip` succeeds and the binary runs `list --format json`.
- No Spectre escape sequences leak into JSON output.
- Piped `copy` with one snippet skips the picker; with many it shows the picker over only the piped subset.

