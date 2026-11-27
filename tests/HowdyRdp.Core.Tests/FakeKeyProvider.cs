using System.Security.Cryptography;
using HowdyRdp.Core.Vault;

namespace HowdyRdp.Core.Tests;

/// <summary>Windows Hello の代わり。HMAC で決まった署名を返す。</summary>
internal sealed class FakeKeyProvider : IVaultKeyProvider
{
    private byte[]? _secret;

    public int SignCount { get; private set; }

    public int CreateCount { get; private set; }

    public bool CancelNext { get; set; }

    public bool Supported { get; set; } = true;

    public Task<VaultKeyStatus> GetStatusAsync() => Task.FromResult(
        !Supported ? VaultKeyStatus.Unsupported : _secret is null ? VaultKeyStatus.Missing : VaultKeyStatus.Ready);

    public Task CreateAsync()
    {
        CreateCount++;
        _secret = RandomNumberGenerator.GetBytes(32);
        return Task.CompletedTask;
    }

    public Task<byte[]> SignAsync(byte[] data)
    {
        SignCount++;
        if (CancelNext)
        {
            CancelNext = false;
            throw new VaultKeyException(VaultKeyError.Cancelled, "cancelled");
        }

        if (_secret is null)
        {
            throw new VaultKeyException(VaultKeyError.KeyMissing, "missing");
        }

        // 実物は RSA 2048 の署名 (256 バイト)。長さだけ似せる。
        var mac = HMACSHA512.HashData(_secret, data);
        return Task.FromResult<byte[]>([.. mac, .. mac, .. mac, .. mac]);
    }

    public Task DeleteAsync()
    {
        _secret = null;
        return Task.CompletedTask;
    }

    /// <summary>鍵が別物に変わった状況 (再インストール後に作り直した等) を作る。</summary>
    public void ReplaceSecret() => _secret = RandomNumberGenerator.GetBytes(32);
}
