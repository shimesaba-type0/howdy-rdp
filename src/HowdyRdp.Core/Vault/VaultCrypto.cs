using System.Security.Cryptography;
using System.Text;

namespace HowdyRdp.Core.Vault;

/// <summary>内蔵保管庫の暗号化の部品。OS に依存しない。</summary>
public static class VaultCrypto
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int SaltSize = 32;

    private static readonly byte[] ChallengePrefix = Encoding.ASCII.GetBytes("howdy-rdp/vault/challenge/v1");
    private static readonly byte[] KdfInfo = Encoding.ASCII.GetBytes("howdy-rdp/vault/key/v1");
    private static readonly byte[] CheckPlaintext = Encoding.ASCII.GetBytes("howdy-rdp/vault/check/v1");
    private static readonly byte[] CheckAad = Encoding.ASCII.GetBytes("check");

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>Hello の鍵に署名してもらうデータ。保管庫ごとの乱数を含める。</summary>
    public static byte[] BuildChallenge(byte[] salt) => [.. ChallengePrefix, .. salt];

    /// <summary>署名から暗号化の鍵を作る (HKDF-SHA256)。</summary>
    public static byte[] DeriveKey(byte[] signature, byte[] salt)
    {
        if (signature.Length < 32)
        {
            throw new ArgumentException("署名が短すぎます。", nameof(signature));
        }

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, signature, KeySize, salt, KdfInfo);
    }

    public static VaultItem Encrypt(byte[] key, byte[] plaintext, byte[] aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = new byte[plaintext.Length + TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, data.AsSpan(0, plaintext.Length), data.AsSpan(plaintext.Length), aad);
        return new VaultItem { Nonce = nonce, Data = data };
    }

    /// <exception cref="AuthenticationTagMismatchException">鍵が違うか、データが壊れている。</exception>
    public static byte[] Decrypt(byte[] key, VaultItem item, byte[] aad)
    {
        if (item.Nonce.Length != NonceSize || item.Data.Length < TagSize)
        {
            throw new CryptographicException("保管庫のデータが壊れています。");
        }

        var length = item.Data.Length - TagSize;
        var plaintext = new byte[length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(item.Nonce, item.Data.AsSpan(0, length), item.Data.AsSpan(length), plaintext, aad);
        return plaintext;
    }

    public static VaultItem CreateCheck(byte[] key) => Encrypt(key, CheckPlaintext, CheckAad);

    public static bool VerifyCheck(byte[] key, VaultItem check)
    {
        try
        {
            var plain = Decrypt(key, check, CheckAad);
            return CryptographicOperations.FixedTimeEquals(plain, CheckPlaintext);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static byte[] ItemAad(Guid hostId) => Encoding.ASCII.GetBytes("host:" + hostId.ToString("D"));
}
