using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Extensions;
using CSnip.Handlers;
using TextCopy;

// Set DOTNET_ENVIRONMENT=Development to activate appsettings.Development.json and debug-level logging.
var builder = Host.CreateApplicationBuilder();

// Suppress "Application started / stopped" messages from the host lifetime.
builder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);

// Spectre.Console — IAnsiConsole is the primary output abstraction for command handlers.
builder.Services.AddSingleton(AnsiConsole.Console);

builder.Services.AddSingleton<IClipboardService, ClipboardServiceAbstraction>();

builder.Services.AddCommandHandlers();

var host = builder.Build();

// RootCommand already includes --help and --version by default.
var rootCommand = new RootCommand("csnip — code snippet manager");

// copy command: prompts for text and copies it to the clipboard.
var copyCommand = new Command("copy", "Copy text to the clipboard");
copyCommand.SetActionHandler<CopyCommandHandler, CopyCommandHandler.Symbols>(host.Services);

rootCommand.Add(copyCommand);

return await rootCommand
    .Parse(args)
    .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = true });
