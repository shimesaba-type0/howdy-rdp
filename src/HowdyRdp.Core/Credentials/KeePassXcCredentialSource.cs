using HowdyRdp.Core.Models;

namespace HowdyRdp.Core.Credentials;

/// <summary>
/// KeePassXC からの取り出し。(未実装)
///
/// 予定している作り:
/// - KeePassXC の「ブラウザ統合」と同じプロトコル (keepassxc-proxy 経由の名前付きパイプ、NaCl box で暗号化) で話す
/// - 初回は associate で KeePassXC 側の承認を 1 回もらい、ID と鍵を保存する
/// - get-logins で rdp://ホスト名 に一致するエントリを探す
/// - KeePassXC がロック中なら解除を求める
/// - Windows Hello の確認は KeePassXC に問い合わせる前に出す
///
/// RDP 用のエントリ (rdp:// の URL) でこのプロトコルが期待どおり動くかは、まだ確かめていない。
/// </summary>
public sealed class KeePassXcCredentialSource : ICredentialSource
{
    public CredentialSourceKind Kind => CredentialSourceKind.KeePassXc;

    public string DisplayName => "KeePassXC";

    public Task<CredentialResult> GetPasswordAsync(HostEntry host, CancellationToken cancellationToken = default) =>
        Task.FromResult(CredentialResult.Unavailable(
            "KeePassXC との連携はまだ作っていません (次の段階で作ります)。いまは内蔵の保管先を使ってください。"));
}
