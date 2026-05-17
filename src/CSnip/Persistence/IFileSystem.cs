namespace CSnip.Persistence;

public interface IFileSystem
{
    bool FileExists(string path);
    Stream OpenRead(string path);
    Stream Create(string path);
    void CreateDirectory(string path);
}
