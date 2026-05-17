namespace CSnip.Persistence;

public class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public Stream Create(string path) => File.Create(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
