using System.CommandLine;
using CSnip.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace CSnip.Extensions;

public static class CommandExtensions
{
    public static void SetActionHandler<TCommandHandler, TSymbols>(this Command command, IServiceProvider serviceProvider)
        where TCommandHandler : ICliCommandHandler
        where TSymbols : ICommandSymbols, new()
    {
        var symbols = new TSymbols();

        foreach (var t in symbols.Arguments)
            command.Add(t);

        foreach (var t in symbols.Options)
            command.Add(t);

        command.SetAction((result, token) =>
        {
            using var scope = serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TCommandHandler>();
            return handler.HandleAsync(result, token);
        });
    }
}
