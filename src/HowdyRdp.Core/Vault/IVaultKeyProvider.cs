namespace HowdyRdp.Core.Vault;

public enum VaultKeyStatus
{
    /// <summary>このPCの鍵がある。</summary>
    Ready,

    /// <summary>鍵がまだない (初回) か、消えた (再インストールや PIN のリセット)。</summary>
    Missing,

    /// <summary>Windows Hello が設定されていない、または使えない。</summary>
    Unsupported,
}

/// <summary>
/// 内蔵保管庫の鍵の元になる「署名」を作るもの。
/// 本番は Windows Hello (KeyCredentialManager、秘密鍵は TPM の中)。テストでは偽物に差し替える。
/// </summary>
public interface IVaultKeyProvider
{
    Task<VaultKeyStatus> GetStatusAsync();

    /// <summary>鍵を新しく作る。Hello の確認が出る。すでにあれば置き換える。</summary>
    Task CreateAsync();

    /// <summary>
    /// 決まったデータに署名する。Hello の確認が出る。
    /// 同じ鍵・同じデータなら毎回同じ署名が返ることが前提 (RSA PKCS#1 v1.5)。
    /// </summary>
    /// <exception cref="VaultKeyException">キャンセル、鍵がない、Hello が使えない。</exception>
    Task<byte[]> SignAsync(byte[] data);

    Task DeleteAsync();
}

public enum VaultKeyError
{
    Cancelled,
    KeyMissing,
    Unsupported,
    KeyMismatch,
    Other,
}

public sealed class VaultKeyException : Exception
{
    public VaultKeyException(VaultKeyError error, string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
    }

    public VaultKeyError Error { get; }
}
