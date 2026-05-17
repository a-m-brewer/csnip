using CSnip.Models;

namespace CSnip.Services;

public interface ITemplateService
{
    IReadOnlyList<TemplateVariable> ParseTemplates(string command);
    string ApplyTemplates(string command, IReadOnlyDictionary<string, string> values);
}
