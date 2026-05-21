namespace CSnip.Persistence;

public interface IUserConfigInitialiser
{
    string ConfigPath { get; }
    void EnsureInitialised();
}
