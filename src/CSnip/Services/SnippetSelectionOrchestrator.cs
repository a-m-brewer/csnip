using CSnip.Models;

namespace CSnip.Services;

public class SnippetSelectionOrchestrator(
    ISnippetPromptService snippetPromptService,
    ITemplatePromptService templatePromptService,
    ITemplateService templateService) : ISnippetSelectionOrchestrator
{
    public async Task<string?> ResolveSnippetCommandAsync(
        IEnumerable<Snippet> snippets,
        CancellationToken cancellationToken)
    {
        var selected = await snippetPromptService.SelectSnippetAsync(snippets, cancellationToken);
        if (selected is null) return null;

        var templates = templateService.ParseTemplates(selected.Command);
        if (templates.Count == 0) return selected.Command;

        var values = await templatePromptService.PromptForTemplatesAsync(templates, cancellationToken);
        return templateService.ApplyTemplates(selected.Command, values);
    }
}
