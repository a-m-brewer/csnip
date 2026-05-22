using CSnip.Abstractions;
using CSnip.Handlers;
using CSnip.Models.Settings;
using CSnip.Persistence;
using CSnip.Pipeline;
using CSnip.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CSnip.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddSingleton<ILineReader, ConsoleLineReader>();
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<ISnippetRepository, SnippetRepository>();
        services.AddOptions<StoreSettings>();

        services.AddSingleton<IPipelineReader, PipelineReader>();
        services.AddSingleton<IPipelineWriter, PipelineWriter>();
        services.AddSingleton<FormatResolver>();

        services.AddTransient<ITemplateService, TemplateService>();
        services.AddTransient<ISnippetPromptService, SpectreSnippetPromptService>();
        services.AddTransient<ITemplatePromptService, SpectreTemplatePromptService>();
        services.AddTransient<ISnippetSelectionOrchestrator, SnippetSelectionOrchestrator>();

        services.AddTransient<AddCommandHandler>();
        services.AddTransient<EditCommandHandler>();
        services.AddTransient<DeleteCommandHandler>();
        services.AddTransient<CopyCommandHandler>();
        services.AddTransient<ExecCommandHandler>();
        services.AddTransient<ListCommandHandler>();
        services.AddTransient<SshCommandHandler>();

        return services;
    }
}
