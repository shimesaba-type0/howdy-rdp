using HowdyRdp.Core.Credentials;
using HowdyRdp.Core.Models;
using HowdyRdp.Core.Vault;

namespace HowdyRdp.Core.Tests;

public class BuiltInVaultTests
{
    // テスト用の値。実在のホストやパスワードではない。
    private const string SamplePassword = "correct horse battery staple";

    [Fact]
    public async Task SaveThenRead_ReturnsSamePassword()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var vault = new BuiltInVault(dir.Paths, keys);
        var id = Guid.NewGuid();

        await vault.SetPasswordAsync(id, SamplePassword);

        Assert.True(vault.HasPassword(id));
        Assert.Equal(1, keys.CreateCount);
        Assert.Equal(SamplePassword, await vault.GetPasswordAsync(id));
    }

    [Fact]
    public async Task VaultFile_DoesNotContainPlaintext()
    {
        using var dir = new TempDir();
        var vault = new BuiltInVault(dir.Paths, new FakeKeyProvider());
        await vault.SetPasswordAsync(Guid.NewGuid(), SamplePassword);

        var text = await File.ReadAllTextAsync(dir.Paths.VaultFile);
        Assert.DoesNotContain(SamplePassword, text);
        Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SamplePassword)), text);
    }

    [Fact]
    public async Task EveryRead_AsksForHello_WhenSkipIsZero()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var vault = new BuiltInVault(dir.Paths, keys);
        var id = Guid.NewGuid();
        await vault.SetPasswordAsync(id, SamplePassword);

        var before = keys.SignCount;
        await vault.GetPasswordAsync(id);
        await vault.GetPasswordAsync(id);

        Assert.Equal(before + 2, keys.SignCount);
    }

    [Fact]
    public async Task SkipDuration_ReusesKeyUntilExpiry()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var vault = new BuiltInVault(dir.Paths, keys, () => now) { SkipDuration = TimeSpan.FromMinutes(5) };
        var id = Guid.NewGuid();
        await vault.SetPasswordAsync(id, SamplePassword);

        var before = keys.SignCount;
        now += TimeSpan.FromMinutes(4);
        Assert.Equal(SamplePassword, await vault.GetPasswordAsync(id));
        Assert.Equal(before, keys.SignCount);

        now += TimeSpan.FromMinutes(2);
        Assert.Equal(SamplePassword, await vault.GetPasswordAsync(id));
        Assert.Equal(before + 1, keys.SignCount);
    }

    [Fact]
    public async Task ClearCache_ForcesHelloAgain()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var vault = new BuiltInVault(dir.Paths, keys) { SkipDuration = TimeSpan.FromMinutes(15) };
        var id = Guid.NewGuid();
        await vault.SetPasswordAsync(id, SamplePassword);

        var before = keys.SignCount;
        vault.ClearCache();
        await vault.GetPasswordAsync(id);
        Assert.Equal(before + 1, keys.SignCount);
    }

    [Fact]
    public async Task ChangedKey_IsReportedAsMismatch()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var vault = new BuiltInVault(dir.Paths, keys);
        var id = Guid.NewGuid();
        await vault.SetPasswordAsync(id, SamplePassword);

        keys.ReplaceSecret();

        var ex = await Assert.ThrowsAsync<VaultKeyException>(() => vault.GetPasswordAsync(id));
        Assert.Equal(VaultKeyError.KeyMismatch, ex.Error);
    }

    [Fact]
    public async Task SwappedItems_DoNotDecrypt()
    {
        using var dir = new TempDir();
        var vault = new BuiltInVault(dir.Paths, new FakeKeyProvider());
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await vault.SetPasswordAsync(a, "aaaa");
        await vault.SetPasswordAsync(b, "bbbb");

        // ファイルを書き換えて、a の欄に b の暗号文を入れる
        var json = await File.ReadAllTextAsync(dir.Paths.VaultFile);
        var doc = System.Text.Json.JsonSerializer.Deserialize<VaultDocument>(json)!;
        doc.Items[a.ToString("D")] = doc.Items[b.ToString("D")];
        await File.WriteAllTextAsync(dir.Paths.VaultFile, System.Text.Json.JsonSerializer.Serialize(doc));

        await Assert.ThrowsAsync<VaultKeyException>(() => vault.GetPasswordAsync(a));
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatHost()
    {
        using var dir = new TempDir();
        var vault = new BuiltInVault(dir.Paths, new FakeKeyProvider());
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await vault.SetPasswordAsync(a, "aaaa");
        await vault.SetPasswordAsync(b, "bbbb");

        vault.Remove(a);

        Assert.False(vault.HasPassword(a));
        Assert.Equal("bbbb", await vault.GetPasswordAsync(b));
    }

    [Fact]
    public async Task Reset_RemovesAllPasswordsAndMakesNewKey()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var vault = new BuiltInVault(dir.Paths, keys);
        var id = Guid.NewGuid();
        await vault.SetPasswordAsync(id, SamplePassword);

        await vault.ResetAsync();

        Assert.False(vault.HasPassword(id));
        Assert.True(vault.IsInitialized);
        Assert.Equal(2, keys.CreateCount);
    }

    [Fact]
    public async Task CredentialSource_MapsCancelAndMissing()
    {
        using var dir = new TempDir();
        var keys = new FakeKeyProvider();
        var vault = new BuiltInVault(dir.Paths, keys);
        var source = new BuiltInCredentialSource(vault);
        var host = new HostEntry { HostName = "host.example.invalid", UserName = "user" };

        Assert.Equal(CredentialStatus.NotFound, (await source.GetPasswordAsync(host)).Status);

        await vault.SetPasswordAsync(host.Id, SamplePassword);
        keys.CancelNext = true;
        Assert.Equal(CredentialStatus.Cancelled, (await source.GetPasswordAsync(host)).Status);

        var ok = await source.GetPasswordAsync(host);
        Assert.Equal(CredentialStatus.Ok, ok.Status);
        Assert.Equal(SamplePassword, ok.Password);
    }

    [Fact]
    public async Task Unsupported_Hello_IsReported()
    {
        using var dir = new TempDir();
        var vault = new BuiltInVault(dir.Paths, new FakeKeyProvider { Supported = false });

        var ex = await Assert.ThrowsAsync<VaultKeyException>(() => vault.SetPasswordAsync(Guid.NewGuid(), SamplePassword));
        Assert.Equal(VaultKeyError.Unsupported, ex.Error);
    }
}
