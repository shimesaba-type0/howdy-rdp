namespace HowdyRdp.Core.Storage;

/// <summary>設定ファイルの置き場所。既定は %APPDATA%\howdy-rdp。</summary>
public sealed class AppPaths
{
    public AppPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public string HostsFile => Path.Combine(RootDirectory, "hosts.json");

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string VaultFile => Path.Combine(RootDirectory, "vault.json");

    public static AppPaths Default() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "howdy-rdp"));
}
