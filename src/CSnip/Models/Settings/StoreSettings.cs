namespace CSnip.Models.Settings;

public class StoreSettings
{
    public string SnippetsPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "csnip",
        "snippets.json");
}
