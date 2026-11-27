namespace HowdyRdp.Core.Models;

/// <summary>パスワードをどこから取り出すか。ホストごとに選ぶ。</summary>
public enum CredentialSourceKind
{
    /// <summary>ツール内蔵の保管庫 (Windows Hello の鍵で暗号化)。</summary>
    BuiltIn = 0,

    /// <summary>KeePassXC (ブラウザ統合のプロトコルで rdp://ホスト名 のエントリを探す)。</summary>
    KeePassXc = 1,
}

/// <summary>
/// 接続先 1 件。ここにはパスワードを持たない (hosts.json にそのまま書かれるため)。
/// </summary>
public sealed class HostEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = "";

    public string HostName { get; set; } = "";

    public int Port { get; set; } = 3389;

    /// <summary>"DOMAIN\user" または "user" または "user@domain"。</summary>
    public string UserName { get; set; } = "";

    public CredentialSourceKind Source { get; set; } = CredentialSourceKind.BuiltIn;

    public DateTimeOffset? LastConnected { get; set; }

    /// <summary>KeePassXC で照合するときの URL。</summary>
    public string KeePassXcUrl => $"rdp://{HostName}";

    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? HostName : DisplayName;

    /// <summary>"DOMAIN\user" を (domain, user) に分ける。UPN 形式はそのまま user に入れる。</summary>
    public (string? Domain, string User) SplitUserName()
    {
        var name = UserName.Trim();
        var i = name.IndexOf('\\');
        if (i > 0 && i < name.Length - 1)
        {
            return (name[..i], name[(i + 1)..]);
        }

        return (null, name);
    }

    public HostEntry Clone() => (HostEntry)MemberwiseClone();
}
