namespace HowdyRdp.Core.Vault;

/// <summary>
/// vault.json の中身。パスワードはホストごとに AES-256-GCM で暗号化して持つ。
/// 平文のパスワードと暗号化の鍵はファイルに書かない。
/// </summary>
public sealed class VaultDocument
{
    public int Version { get; set; } = 1;

    /// <summary>保管庫ごとの乱数。署名する対象と鍵の導出 (HKDF) の両方に使う。</summary>
    public byte[] Salt { get; set; } = [];

    /// <summary>決まった文字列を暗号化したもの。鍵が合っているかの確認用。</summary>
    public VaultItem Check { get; set; } = new();

    /// <summary>キーはホストの Id。</summary>
    public Dictionary<string, VaultItem> Items { get; set; } = [];
}

public sealed class VaultItem
{
    public byte[] Nonce { get; set; } = [];

    /// <summary>暗号文の後ろに 16 バイトの認証タグをつなげたもの。</summary>
    public byte[] Data { get; set; } = [];
}
