using CSnip.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace CSnip.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<ICliCommandHandler>()
            .AddClasses(c => c.AssignableTo<ICliCommandHandler>())
            .AsSelfWithInterfaces()
            .WithTransientLifetime());

        return services;
    }
}
