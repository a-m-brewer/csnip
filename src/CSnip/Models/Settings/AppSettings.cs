namespace CSnip.Models.Settings;

public class AppSettings
{
    public SshSettings Ssh { get; init; } = new();
}
