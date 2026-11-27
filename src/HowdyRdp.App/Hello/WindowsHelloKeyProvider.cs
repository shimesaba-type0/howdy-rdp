using System.Runtime.InteropServices;
using HowdyRdp.Core.Vault;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;

namespace HowdyRdp.App.Hello;

/// <summary>
/// Windows Hello (KeyCredentialManager) で内蔵保管庫の鍵の元になる署名を作る。
/// 秘密鍵は TPM の中に作られ、外には出ない。署名のたびに Hello の確認が出る。
/// </summary>
internal sealed class WindowsHelloKeyProvider : IVaultKeyProvider
{
    // このツール専用の鍵の名前。ユーザーごと・PC ごとに 1 つ。
    private const string KeyName = "howdy-rdp-vault-v1";

    public async Task<VaultKeyStatus> GetStatusAsync()
    {
        if (!await KeyCredentialManager.IsSupportedAsync())
        {
            return VaultKeyStatus.Unsupported;
        }

        var result = await KeyCredentialManager.OpenAsync(KeyName);
        return result.Status switch
        {
            KeyCredentialStatus.Success => VaultKeyStatus.Ready,
            KeyCredentialStatus.NotFound => VaultKeyStatus.Missing,
            _ => VaultKeyStatus.Unsupported,
        };
    }

    public async Task CreateAsync()
    {
        using var _ = HelloDialogFocus.Start();
        var result = await KeyCredentialManager.RequestCreateAsync(KeyName, KeyCredentialCreationOption.ReplaceExisting);
        ThrowIfFailed(result.Status, "鍵を作れませんでした");
    }

    public async Task<byte[]> SignAsync(byte[] data)
    {
        var open = await KeyCredentialManager.OpenAsync(KeyName);
        ThrowIfFailed(open.Status, "鍵を開けませんでした");

        using var _ = HelloDialogFocus.Start();
        var signed = await open.Credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(data));
        ThrowIfFailed(signed.Status, "Windows Hello の確認ができませんでした");

        CryptographicBuffer.CopyToByteArray(signed.Result, out var signature);
        return signature;
    }

    public async Task DeleteAsync()
    {
        try
        {
            await KeyCredentialManager.DeleteAsync(KeyName);
        }
        catch (Exception ex) when (ex is COMException or FileNotFoundException)
        {
            // 鍵がもともとない場合は何もしない
        }
    }

    private static void ThrowIfFailed(KeyCredentialStatus status, string what)
    {
        switch (status)
        {
            case KeyCredentialStatus.Success:
                return;
            case KeyCredentialStatus.UserCanceled:
                throw new VaultKeyException(VaultKeyError.Cancelled, "本人確認がキャンセルされました。");
            case KeyCredentialStatus.NotFound:
                throw new VaultKeyException(VaultKeyError.KeyMissing, "このPCの鍵が見つかりません。");
            case KeyCredentialStatus.UserPrefersPassword:
                throw new VaultKeyException(VaultKeyError.Cancelled, "本人確認がキャンセルされました。");
            case KeyCredentialStatus.SecurityDeviceLocked:
                throw new VaultKeyException(VaultKeyError.Other, $"{what}: セキュリティデバイス (TPM) がロックされています。しばらく待ってからやり直してください。");
            default:
                throw new VaultKeyException(VaultKeyError.Other, $"{what} ({status})。");
        }
    }
}
