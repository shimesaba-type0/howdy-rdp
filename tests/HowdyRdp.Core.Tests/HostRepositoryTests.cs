using HowdyRdp.Core.Models;
using HowdyRdp.Core.Storage;

namespace HowdyRdp.Core.Tests;

public class HostRepositoryTests
{
    [Fact]
    public void Upsert_PersistsAcrossInstances()
    {
        using var dir = new TempDir();
        var repo = new HostRepository(dir.Paths);
        var host = new HostEntry
        {
            DisplayName = "検証用",
            HostName = "host.example.invalid",
            Port = 3390,
            UserName = @"EXAMPLE\user",
            Source = CredentialSourceKind.KeePassXc,
        };
        repo.Upsert(host);

        var loaded = new HostRepository(dir.Paths).Find(host.Id);

        Assert.NotNull(loaded);
        Assert.Equal("検証用", loaded.DisplayName);
        Assert.Equal(3390, loaded.Port);
        Assert.Equal(CredentialSourceKind.KeePassXc, loaded.Source);
        Assert.Equal("rdp://host.example.invalid", loaded.KeePassXcUrl);
    }

    [Fact]
    public void HostsFile_HasNoPasswordField()
    {
        using var dir = new TempDir();
        new HostRepository(dir.Paths).Upsert(new HostEntry { HostName = "host.example.invalid" });

        var text = File.ReadAllText(dir.Paths.HostsFile);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"EXAMPLE\user", "EXAMPLE", "user")]
    [InlineData("user", null, "user")]
    [InlineData("user@example.invalid", null, "user@example.invalid")]
    [InlineData(@"\user", null, @"\user")]
    public void SplitUserName_HandlesFormats(string input, string? domain, string user)
    {
        var (d, u) = new HostEntry { UserName = input }.SplitUserName();
        Assert.Equal(domain, d);
        Assert.Equal(user, u);
    }

    [Fact]
    public void Settings_UnknownSkipMinutes_FallsBackToEveryTime()
    {
        using var dir = new TempDir();
        var repo = new SettingsRepository(dir.Paths);
        repo.Save(new AppSettings { HelloSkipMinutes = 7 });

        Assert.Equal(0, new SettingsRepository(dir.Paths).Current.HelloSkipMinutes);
    }
}
