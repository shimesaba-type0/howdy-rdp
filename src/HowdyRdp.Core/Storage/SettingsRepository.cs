using HowdyRdp.Core.Models;

namespace HowdyRdp.Core.Storage;

public sealed class SettingsRepository
{
    private readonly string _path;

    public SettingsRepository(AppPaths paths)
    {
        _path = paths.SettingsFile;
        Current = (JsonFile.Read<AppSettings>(_path) ?? new AppSettings()).Normalize();
    }

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        Current = settings.Normalize();
        JsonFile.Write(_path, Current);
    }
}
