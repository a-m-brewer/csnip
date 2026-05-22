using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Models.Settings;
using CSnip.Persistence;
using CSnip.Pipeline;
using CSnip.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using System.Text;

namespace CSnip.Handlers;

public class SshCommandHandler(
    IAnsiConsole console,
    IShellExecutor shellExecutor,
    ISnippetRepository snippetRepository,
    ISnippetSelectionOrchestrator selectionOrchestrator,
    IConsoleEnvironment consoleEnvironment,
    IPipelineReader pipelineReader,
    IOptions<SshSettings> sshSettings) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols
    {
        public IReadOnlyList<Argument> Arguments { get; } = [Hosts];
        public IReadOnlyList<Option> Options { get; } = [NoHeader, SshProgram, Profile];

        public static readonly Argument<string[]> Hosts = new("hosts")
        {
            Description = "One or more SSH targets (user@host or host); prefix with alias: for per-host program override (ssh: forces plain ssh)",
            Arity = ArgumentArity.OneOrMore,
        };

        public static readonly Option<bool> NoHeader = new("--no-header")
        {
            Description = "Suppress the per-host header (always hidden for a single host)",
        };

        public static readonly Option<string?> SshProgram = new("--ssh-program")
        {
            Description = "SSH program to use for all un-prefixed hosts (e.g. \"hvc ssh\"); mutually exclusive with --profile",
        };

        public static readonly Option<string?> Profile = new("--profile")
        {
            Description = "Named SSH program profile from config (Ssh:Programs) applied to all un-prefixed hosts; mutually exclusive with --ssh-program",
        };
    }

    private record HostEntry(string Program, string Host);

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        // Hosts come first; everything from the first '-'-prefixed token onward is an SSH arg.
        // System.CommandLine 2.x greedily consumes all tokens (including option-like ones) into
        // the OneOrMore argument, so we split manually here.
        var allTokens = result.GetValue(Symbols.Hosts) ?? [];
        var firstOptionIdx = Array.FindIndex(allTokens, t => t.StartsWith('-'));
        var rawHosts = firstOptionIdx < 0 ? allTokens : allTokens[..firstOptionIdx];
        var extraSshArgs = (IReadOnlyList<string>)(firstOptionIdx < 0 ? [] : allTokens[firstOptionIdx..]);

        if (rawHosts.Length == 0)
        {
            console.MarkupLine("[red]At least one host is required.[/]");
            return 1;
        }

        var sshProgram = result.GetValue(Symbols.SshProgram);
        var profile = result.GetValue(Symbols.Profile);

        if (sshProgram is not null && profile is not null)
        {
            console.MarkupLine("[red]--ssh-program and --profile are mutually exclusive.[/]");
            return 1;
        }

        string defaultProgram;
        if (sshProgram is not null)
        {
            defaultProgram = sshProgram;
        }
        else if (profile is not null)
        {
            if (!sshSettings.Value.Programs.TryGetValue(profile, out defaultProgram!))
            {
                console.MarkupLine($"[red]Unknown SSH profile '{Markup.Escape(profile)}'. Define it under Ssh:Programs in appsettings.json.[/]");
                return 1;
            }
        }
        else
        {
            defaultProgram = "ssh";
        }

        var noHeader = result.GetValue(Symbols.NoHeader);

        var hostEntries = new List<HostEntry>(rawHosts.Length);
        foreach (var token in rawHosts)
        {
            if (!TryResolveHost(token, defaultProgram, out var entry, out var error))
            {
                console.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
                return 1;
            }
            hostEntries.Add(entry!);
        }

        var showHeaders = hostEntries.Count > 1 && !noHeader;

        IReadOnlyList<Snippet> candidates;

        if (consoleEnvironment.IsInputRedirected)
        {
            candidates = await pipelineReader.ReadAllAsync(cancellationToken);
            if (candidates.Count == 0)
            {
                console.MarkupLine("[yellow]No snippets on stdin.[/]");
                return 0;
            }

            if (candidates.Count == 1)
            {
                var resolved = await selectionOrchestrator.ResolveCommandAsync(candidates[0], cancellationToken);
                if (resolved is null) return 0;
                return await ExecuteOnHostsAsync(resolved, hostEntries, extraSshArgs, showHeaders, cancellationToken);
            }
        }
        else
        {
            candidates = await snippetRepository.GetAllAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                console.MarkupLine("[yellow]No snippets found. Use 'add' to create one.[/]");
                return 0;
            }
        }

        var command = await selectionOrchestrator.ResolveSnippetCommandAsync(candidates, cancellationToken);
        if (command is null) return 0;

        return await ExecuteOnHostsAsync(command, hostEntries, extraSshArgs, showHeaders, cancellationToken);
    }

    private bool TryResolveHost(string token, string defaultProgram, out HostEntry? entry, out string? error)
    {
        var colonIdx = token.IndexOf(':');
        // Only treat as prefix:host if the part before ':' looks like an identifier
        // (no '@', '[', or '/' which indicate it's part of a host address like user@host or [::1]).
        if (colonIdx > 0 && token.AsSpan()[..colonIdx].IndexOfAny('@', '[', '/') < 0)
        {
            var prefix = token[..colonIdx];
            var host = token[(colonIdx + 1)..];

            string program;
            if (prefix == "ssh")
            {
                program = "ssh";
            }
            else if (sshSettings.Value.Programs.TryGetValue(prefix, out var alias))
            {
                program = alias;
            }
            else
            {
                entry = null;
                error = $"Unknown SSH program alias '{prefix}'. Define it under Ssh:Programs in appsettings.json.";
                return false;
            }

            entry = new HostEntry(program, host);
            error = null;
            return true;
        }

        entry = new HostEntry(defaultProgram, token);
        error = null;
        return true;
    }

    private async Task<int> ExecuteOnHostsAsync(
        string command,
        IReadOnlyList<HostEntry> hostEntries,
        IReadOnlyList<string> extraSshArgs,
        bool showHeaders,
        CancellationToken cancellationToken)
    {
        var failures = new List<(string Host, int ExitCode)>();
        foreach (var entry in hostEntries)
        {
            if (showHeaders)
                console.Write(new Rule($"[blue]{Markup.Escape(entry.Host)}[/]"));

            if (!TrySplitProgram(entry.Program, out var programParts, out var error))
            {
                console.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
                failures.Add((entry.Host, 1));
                continue;
            }

            var fileName = programParts[0];
            var arguments = new List<string>(programParts.Count - 1 + extraSshArgs.Count + 2);
            arguments.AddRange(programParts.Skip(1));
            arguments.AddRange(extraSshArgs);
            arguments.Add(entry.Host);
            arguments.Add(command);

            var code = await shellExecutor.ExecuteAsync(fileName, arguments, cancellationToken);
            if (code != 0)
                failures.Add((entry.Host, code));
        }

        if (failures.Count > 0)
        {
            console.MarkupLine("[red]Failed hosts:[/]");
            foreach (var (host, code) in failures)
                console.MarkupLine($"  [red]{Markup.Escape(host)}[/] (exit {code})");
            return failures[0].ExitCode;
        }

        return 0;
    }

    private static bool TrySplitProgram(string program, out IReadOnlyList<string> parts, out string? error)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        char? quote = null;

        for (var i = 0; i < program.Length; i++)
        {
            var ch = program[i];

            if (quote is not null)
            {
                if (ch == quote)
                {
                    quote = null;
                }
                else if (quote == '"' && ch == '\\' && i + 1 < program.Length && program[i + 1] is '"' or '\\')
                {
                    current.Append(program[++i]);
                }
                else
                {
                    current.Append(ch);
                }

                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else if (ch is '\'' or '"')
            {
                quote = ch;
            }
            else
            {
                current.Append(ch);
            }
        }

        if (quote is not null)
        {
            parts = [];
            error = $"Invalid SSH program '{program}': unmatched {quote} quote.";
            return false;
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        if (result.Count == 0)
        {
            parts = [];
            error = "SSH program cannot be empty.";
            return false;
        }

        parts = result;
        error = null;
        return true;
    }
}
