using CSnip.Models;

namespace CSnip.Services;

public interface ITemplatePromptService
{
    Task<IReadOnlyDictionary<string, string>> PromptForTemplatesAsync(
        IReadOnlyList<TemplateVariable> templates,
        CancellationToken cancellationToken);
}
