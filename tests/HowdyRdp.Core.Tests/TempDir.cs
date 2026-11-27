using HowdyRdp.Core.Storage;

namespace HowdyRdp.Core.Tests;

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "howdy-rdp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Paths = new AppPaths(Path);
    }

    public string Path { get; }

    public AppPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
