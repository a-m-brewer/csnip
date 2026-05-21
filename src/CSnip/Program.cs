using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Extensions;
using CSnip.Handlers;
using CSnip.Models.Settings;
using CSnip.Pipeline;

// Capture console state before any redirection.
var isInputRedirected = Console.IsInputRedirected;
var isOutputRedirected = Console.IsOutputRedirected;

// When stdin is piped: drain it into memory so PipelineReader can access it later.
// TtyConsoleInput opens /dev/tty independently (no dup2 needed), which avoids the
// .NET 10 issue where Console.ReadKey checks the cached IsInputRedirected flag.
TextReader pipeReader = TextReader.Null;
if (isInputRedirected)
    pipeReader = new StringReader(await Console.In.ReadToEndAsync());

// When stdin was piped, Console.IsInputRedirected is cached true so we must force
// Interactive = Yes; TtyAnsiConsole substitutes a custom IAnsiConsoleInput that reads
// directly from /dev/tty via P/Invoke, bypassing Console.ReadKey's cached check.
var ansiConsole = isInputRedirected
    ? new TtyAnsiConsole(AnsiConsole.Create(new AnsiConsoleSettings { Interactive = InteractionSupport.Yes }))
    : AnsiConsole.Console;

var consoleEnv = new ConsoleEnvironmentAbstraction(isInputRedirected, isOutputRedirected, pipeReader, Console.Out);

// Set DOTNET_ENVIRONMENT=Development to activate appsettings.Development.json and debug-level logging.
var builder = Host.CreateApplicationBuilder();

// Suppress "Application started / stopped" messages from the host lifetime.
builder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);
builder.Services.Configure<StoreSettings>(builder.Configuration.GetSection("Store"));

// Spectre.Console — IAnsiConsole is the primary output abstraction for command handlers.
builder.Services.AddSingleton(ansiConsole);
builder.Services.AddSingleton<IConsoleEnvironment>(consoleEnv);

builder.Services.AddSingleton<IClipboardService, ClipboardServiceAbstraction>();
builder.Services.AddSingleton<IShellExecutor, ShellExecutor>();

builder.Services.AddCommandHandlers();

var host = builder.Build();

// RootCommand already includes --help and --version by default.
var rootCommand = new RootCommand("csnip — code snippet manager");

// add command: prompts for a new command to be stored in the system
var addCommand = new Command("add", "Add a new code snippet");
addCommand.SetActionHandler<AddCommandHandler, AddCommandHandler.Symbols>(host.Services);
rootCommand.Add(addCommand);

// list command: list all snippets, pipe-friendly
var listCommand = new Command("list", "List all snippets");
listCommand.SetActionHandler<ListCommandHandler, ListCommandHandler.Symbols>(host.Services);
rootCommand.Add(listCommand);

// copy command: prompts for text and copies it to the clipboard.
var copyCommand = new Command("copy", "Copy text to the clipboard");
copyCommand.SetActionHandler<CopyCommandHandler, CopyCommandHandler.Symbols>(host.Services);
rootCommand.Add(copyCommand);

// exec command: selects a snippet and executes it in a shell.
var execCommand = new Command("exec", "Execute a snippet in a shell");
execCommand.SetActionHandler<ExecCommandHandler, ExecCommandHandler.Symbols>(host.Services);
rootCommand.Add(execCommand);

// ssh command: selects a snippet and executes it on remote hosts via SSH.
var sshCommand = new Command("ssh", "Execute a snippet on remote hosts via SSH");
sshCommand.SetActionHandler<SshCommandHandler, SshCommandHandler.Symbols>(host.Services);
rootCommand.Add(sshCommand);

return await rootCommand
    .Parse(args)
    .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = true });
