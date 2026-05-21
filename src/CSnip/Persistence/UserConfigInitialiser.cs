using System.Text.Json;
using CSnip.Models.Settings;

namespace CSnip.Persistence;

public class UserConfigInitialiser : IUserConfigInitialiser
{
    private readonly IFileSystem _fileSystem;
    private readonly string _configDir;

    public string ConfigPath { get; }

    public UserConfigInitialiser(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "csnip");
        ConfigPath = Path.Combine(_configDir, "appsettings.json");
    }

    public void EnsureInitialised()
    {
        _fileSystem.CreateDirectory(_configDir);

        if (_fileSystem.FileExists(ConfigPath))
            return;

        var defaultConfig = new AppSettings
        {
            Ssh = new SshSettings { Programs = new Dictionary<string, string> { ["hvc"] = "hvc ssh" } }
        };

        var writeOptions = new JsonSerializerOptions { WriteIndented = true };
        using var stream = _fileSystem.Create(ConfigPath);
        JsonSerializer.Serialize(stream, defaultConfig, new AppSettingsJsonContext(writeOptions).AppSettings);
    }
}
