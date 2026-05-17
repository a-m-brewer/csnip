using CSnip.Handlers;
using CSnip.Models.Settings;
using CSnip.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CSnip.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<ISnippetRepository, SnippetRepository>();
        services.AddOptions<StoreSettings>();

        services.Scan(s => s.FromAssemblyOf<ICliCommandHandler>()
            .AddClasses(c => c.AssignableTo<ICliCommandHandler>())
            .AsSelfWithInterfaces()
            .WithTransientLifetime());

        return services;
    }
}
