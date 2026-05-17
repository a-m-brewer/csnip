using CSnip.Models;
using Spectre.Console;

namespace CSnip.Services;

public class SpectreTemplatePromptService(IAnsiConsole console) : ITemplatePromptService
{
    public Task<IReadOnlyDictionary<string, string>> PromptForTemplatesAsync(
        IReadOnlyList<TemplateVariable> templates,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>();

        foreach (var template in templates)
        {
            string value;
            if (template.Default is not null)
            {
                var prompt = new TextPrompt<string>($"[blue]{template.Key}[/]:")
                    .DefaultValue(template.Default)
                    .AllowEmpty();
                var input = console.Prompt(prompt);
                value = string.IsNullOrEmpty(input) ? template.Default : input;
            }
            else
            {
                var prompt = new TextPrompt<string>($"[blue]{template.Key}[/] [red](required)[/]:")
                    .Validate(s => !string.IsNullOrWhiteSpace(s)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Value is required[/]"));
                value = console.Prompt(prompt);
            }

            values[template.Key] = value;
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(values);
    }
}
