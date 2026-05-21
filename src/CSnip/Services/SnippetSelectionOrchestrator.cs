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

        return await ResolveCommandAsync(selected, cancellationToken);
    }

    public async Task<string?> ResolveCommandAsync(Snippet snippet, CancellationToken cancellationToken)
    {
        var templates = templateService.ParseTemplates(snippet.Command);
        if (templates.Count == 0) return snippet.Command;

        var values = await templatePromptService.PromptForTemplatesAsync(templates, cancellationToken);
        return templateService.ApplyTemplates(snippet.Command, values);
    }
}
