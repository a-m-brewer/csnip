using System.Text.RegularExpressions;
using CSnip.Models;

namespace CSnip.Services;

public partial class TemplateService : ITemplateService
{
    // Matches <key> or <key=default>; key may contain letters, digits, hyphens, underscores.
    [GeneratedRegex(@"<([^>=\s]+)(?:=([^>]*))?>")]
    private static partial Regex TemplatePattern();

    public IReadOnlyList<TemplateVariable> ParseTemplates(string command)
    {
        var seen = new HashSet<string>();
        var result = new List<TemplateVariable>();

        foreach (Match match in TemplatePattern().Matches(command))
        {
            var key = match.Groups[1].Value;
            if (!seen.Add(key)) continue;

            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;
            result.Add(new TemplateVariable(key, defaultValue));
        }

        return result;
    }

    public string ApplyTemplates(string command, IReadOnlyDictionary<string, string> values)
    {
        return TemplatePattern().Replace(command, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}
