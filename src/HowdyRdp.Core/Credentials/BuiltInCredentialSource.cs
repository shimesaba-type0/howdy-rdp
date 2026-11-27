using HowdyRdp.Core.Models;
using HowdyRdp.Core.Vault;

namespace HowdyRdp.Core.Credentials;

/// <summary>内蔵保管庫からの取り出し。Windows Hello の確認は保管庫の中で出る。</summary>
public sealed class BuiltInCredentialSource : ICredentialSource
{
    private readonly BuiltInVault _vault;

    public BuiltInCredentialSource(BuiltInVault vault)
    {
        _vault = vault;
    }

    public CredentialSourceKind Kind => CredentialSourceKind.BuiltIn;

    public string DisplayName => "内蔵";

    public async Task<CredentialResult> GetPasswordAsync(HostEntry host, CancellationToken cancellationToken = default)
    {
        if (!_vault.HasPassword(host.Id))
        {
            return CredentialResult.NotFound("このホストのパスワードはまだ保存されていません。");
        }

        try
        {
            var password = await _vault.GetPasswordAsync(host.Id).ConfigureAwait(true);
            return password is null
                ? CredentialResult.NotFound("このホストのパスワードはまだ保存されていません。")
                : CredentialResult.Success(password);
        }
        catch (VaultKeyException ex) when (ex.Error == VaultKeyError.Cancelled)
        {
            return CredentialResult.Cancelled();
        }
        catch (VaultKeyException ex) when (ex.Error is VaultKeyError.KeyMissing or VaultKeyError.KeyMismatch)
        {
            return CredentialResult.Unavailable(
                "このPCの鍵が見つかりません。Windows の再インストールや Hello・PIN のリセットで鍵が消えた可能性があります。" +
                "設定から鍵を作り直し、パスワードを入れ直してください。");
        }
        catch (VaultKeyException ex) when (ex.Error == VaultKeyError.Unsupported)
        {
            return CredentialResult.Unavailable(
                "Windows Hello が設定されていません。Windows の設定 > アカウント > サインイン オプション で顔・指紋・PIN のどれかを設定してください。");
        }
        catch (VaultKeyException ex)
        {
            return CredentialResult.Failed(ex.Message);
        }
    }
}
