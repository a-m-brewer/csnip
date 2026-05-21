using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Pipeline;
using CSnip.Services;
using Spectre.Console;

namespace CSnip.Handlers;

public class SshCommandHandler(
    IAnsiConsole console,
    IShellExecutor shellExecutor,
    ISnippetRepository snippetRepository,
    ISnippetSelectionOrchestrator selectionOrchestrator,
    IConsoleEnvironment consoleEnvironment,
    IPipelineReader pipelineReader) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols
    {
        public IReadOnlyList<Argument> Arguments { get; } = [Hosts];
        public IReadOnlyList<Option> Options { get; } = [NoHeader];

        public static readonly Argument<string[]> Hosts = new("hosts")
        {
            Description = "One or more SSH targets (user@host or host)",
            Arity = ArgumentArity.OneOrMore,
        };

        public static readonly Option<bool> NoHeader = new("--no-header")
        {
            Description = "Suppress the per-host header (always hidden for a single host)",
        };
    }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        // Hosts come first; everything from the first '-'-prefixed token onward is an SSH arg.
        // System.CommandLine 2.x greedily consumes all tokens (including option-like ones) into
        // the OneOrMore argument, so we split manually here.
        var allTokens = result.GetValue(Symbols.Hosts) ?? [];
        var firstOptionIdx = Array.FindIndex(allTokens, t => t.StartsWith('-'));
        var hosts = firstOptionIdx < 0 ? allTokens : allTokens[..firstOptionIdx];
        var extraSshArgs = (IReadOnlyList<string>)(firstOptionIdx < 0 ? [] : allTokens[firstOptionIdx..]);

        if (hosts.Length == 0)
        {
            console.MarkupLine("[red]At least one host is required.[/]");
            return 1;
        }

        var noHeader = result.GetValue(Symbols.NoHeader);
        var showHeaders = hosts.Length > 1 && !noHeader;

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
                return await ExecuteOnHostsAsync(resolved, hosts, extraSshArgs, showHeaders, cancellationToken);
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

        return await ExecuteOnHostsAsync(command, hosts, extraSshArgs, showHeaders, cancellationToken);
    }

    private async Task<int> ExecuteOnHostsAsync(
        string command,
        string[] hosts,
        IReadOnlyList<string> extraSshArgs,
        bool showHeaders,
        CancellationToken cancellationToken)
    {
        var quotedCommand = "'" + command.Replace("'", "'\\''") + "'";
        var extraArgsPart = extraSshArgs.Count > 0
            ? string.Join(" ", extraSshArgs.Select(QuoteArgForShell)) + " "
            : "";

        var failures = new List<(string Host, int ExitCode)>();
        foreach (var host in hosts)
        {
            if (showHeaders)
                console.Write(new Rule($"[blue]{Markup.Escape(host)}[/]"));

            var sshCommand = $"ssh {extraArgsPart}{host} {quotedCommand}";
            var code = await shellExecutor.ExecuteAsync(sshCommand, cancellationToken);
            if (code != 0)
                failures.Add((host, code));
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

    private static string QuoteArgForShell(string arg) =>
        "'" + arg.Replace("'", "'\\''") + "'";
}
