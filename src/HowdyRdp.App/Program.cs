using HowdyRdp.App.Forms;
using HowdyRdp.App.Hello;
using HowdyRdp.Core.Credentials;
using HowdyRdp.Core.Storage;
using HowdyRdp.Core.Vault;

namespace HowdyRdp.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var paths = AppPaths.Default();
        var settings = new SettingsRepository(paths);
        var hosts = new HostRepository(paths);
        var vault = new BuiltInVault(paths, new WindowsHelloKeyProvider())
        {
            SkipDuration = TimeSpan.FromMinutes(settings.Current.HelloSkipMinutes),
        };
        var sources = new CredentialSourceRegistry(
        [
            new BuiltInCredentialSource(vault),
            new KeePassXcCredentialSource(),
        ]);

        Application.Run(new MainForm(hosts, settings, vault, sources));
    }
}
