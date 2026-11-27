using System.Security.Cryptography;
using System.Text;
using HowdyRdp.Core.Storage;

namespace HowdyRdp.Core.Vault;

/// <summary>
/// 内蔵保管庫。パスワードを取り出すときと保存するときに、鍵の提供元 (Windows Hello) に署名を求める。
/// 省略時間が設定されていれば、その間だけ導出した鍵をメモリに置く。
/// </summary>
public sealed class BuiltInVault
{
    private readonly string _path;
    private readonly IVaultKeyProvider _keyProvider;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();

    private byte[]? _cachedKey;
    private DateTimeOffset _cachedUntil;

    public BuiltInVault(AppPaths paths, IVaultKeyProvider keyProvider, Func<DateTimeOffset>? now = null)
    {
        _path = paths.VaultFile;
        _keyProvider = keyProvider;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Hello を省略する時間。0 なら毎回確認する。</summary>
    public TimeSpan SkipDuration { get; set; } = TimeSpan.Zero;

    public bool IsInitialized => Load() is { Salt.Length: > 0 };

    public int Count => Load()?.Items.Count ?? 0;

    public bool HasPassword(Guid hostId) => Load()?.Items.ContainsKey(Key(hostId)) == true;

    public Task<VaultKeyStatus> GetKeyStatusAsync() => _keyProvider.GetStatusAsync();

    /// <summary>このホストのパスワードを取り出す。保存されていなければ null。</summary>
    public async Task<string?> GetPasswordAsync(Guid hostId)
    {
        var doc = Load();
        if (doc is null || !doc.Items.TryGetValue(Key(hostId), out var item))
        {
            return null;
        }

        var key = await GetKeyAsync(doc).ConfigureAwait(true);
        byte[]? plain = null;
        try
        {
            plain = VaultCrypto.Decrypt(key, item, VaultCrypto.ItemAad(hostId));
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException ex)
        {
            throw new VaultKeyException(VaultKeyError.Other, "このホストのパスワードを復号できませんでした。保存し直してください。", ex);
        }
        finally
        {
            if (plain is not null)
            {
                CryptographicOperations.ZeroMemory(plain);
            }

            ReleaseKey(key);
        }
    }

    /// <summary>パスワードを暗号化して保存する。初回は鍵の作成と保管庫の初期化も行う。</summary>
    public async Task SetPasswordAsync(Guid hostId, string password)
    {
        var doc = Load();
        byte[] key;
        if (doc is null || doc.Salt.Length == 0)
        {
            (doc, key) = await InitializeAsync().ConfigureAwait(true);
        }
        else
        {
            key = await GetKeyAsync(doc).ConfigureAwait(true);
        }

        var plain = Encoding.UTF8.GetBytes(password);
        try
        {
            doc.Items[Key(hostId)] = VaultCrypto.Encrypt(key, plain, VaultCrypto.ItemAad(hostId));
            JsonFile.Write(_path, doc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            ReleaseKey(key);
        }
    }

    /// <summary>このホストのパスワードを消す。Hello は不要。</summary>
    public void Remove(Guid hostId)
    {
        var doc = Load();
        if (doc is not null && doc.Items.Remove(Key(hostId)))
        {
            JsonFile.Write(_path, doc);
        }
    }

    /// <summary>
    /// 鍵を作り直す。保存済みのパスワードはすべて消える (古い鍵がないと復号できないため)。
    /// </summary>
    public async Task ResetAsync()
    {
        ClearCache();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        await _keyProvider.DeleteAsync().ConfigureAwait(true);
        var (_, key) = await InitializeAsync().ConfigureAwait(true);
        ReleaseKey(key);
    }

    /// <summary>メモリに置いた鍵を消す。PC のロック時や終了時に呼ぶ。</summary>
    public void ClearCache()
    {
        lock (_gate)
        {
            if (_cachedKey is not null)
            {
                CryptographicOperations.ZeroMemory(_cachedKey);
                _cachedKey = null;
            }

            _cachedUntil = DateTimeOffset.MinValue;
        }
    }

    /// <summary>鍵 (なければ作る) で新しい保管庫を作り、文書と導出した鍵を返す。鍵は呼び出し側で消す。</summary>
    private async Task<(VaultDocument Doc, byte[] Key)> InitializeAsync()
    {
        var status = await _keyProvider.GetStatusAsync().ConfigureAwait(true);
        if (status == VaultKeyStatus.Unsupported)
        {
            throw new VaultKeyException(VaultKeyError.Unsupported, "Windows Hello が設定されていません。");
        }

        if (status == VaultKeyStatus.Missing)
        {
            await _keyProvider.CreateAsync().ConfigureAwait(true);
        }

        var salt = VaultCrypto.NewSalt();
        var signature = await _keyProvider.SignAsync(VaultCrypto.BuildChallenge(salt)).ConfigureAwait(true);
        var key = VaultCrypto.DeriveKey(signature, salt);
        CryptographicOperations.ZeroMemory(signature);

        var doc = new VaultDocument { Salt = salt, Check = VaultCrypto.CreateCheck(key) };
        JsonFile.Write(_path, doc);
        Remember(key);
        return (doc, key);
    }

    private async Task<byte[]> GetKeyAsync(VaultDocument doc)
    {
        lock (_gate)
        {
            if (_cachedKey is not null && _now() < _cachedUntil)
            {
                return (byte[])_cachedKey.Clone();
            }
        }

        var signature = await _keyProvider.SignAsync(VaultCrypto.BuildChallenge(doc.Salt)).ConfigureAwait(true);
        var key = VaultCrypto.DeriveKey(signature, doc.Salt);
        CryptographicOperations.ZeroMemory(signature);

        if (!VaultCrypto.VerifyCheck(key, doc.Check))
        {
            CryptographicOperations.ZeroMemory(key);
            throw new VaultKeyException(
                VaultKeyError.KeyMismatch,
                "このPCの鍵が保管庫を作ったときの鍵と違います。鍵を作り直して、パスワードを入れ直してください。");
        }

        Remember(key);
        return key;
    }

    private void Remember(byte[] key)
    {
        lock (_gate)
        {
            if (SkipDuration <= TimeSpan.Zero)
            {
                return;
            }

            if (_cachedKey is not null)
            {
                CryptographicOperations.ZeroMemory(_cachedKey);
            }

            _cachedKey = (byte[])key.Clone();
            _cachedUntil = _now() + SkipDuration;
        }
    }

    private static void ReleaseKey(byte[] key) => CryptographicOperations.ZeroMemory(key);

    private VaultDocument? Load() => JsonFile.Read<VaultDocument>(_path);

    private static string Key(Guid hostId) => hostId.ToString("D");
}
