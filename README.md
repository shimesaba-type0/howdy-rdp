# howdy-rdp

接続のたびに Windows Hello で本人確認してから RDP する、Windows 11 向けのリモートデスクトップ クライアントです。

> パスワードを安全に保存して、安全に取り出して RDP するだけ。

- パスワードは Windows の資格情報マネージャーに入れません。`.rdp` をダブルクリックして Hello を素通りされないようにするためです。
- 内蔵の保管庫は、Windows Hello の鍵 (秘密鍵は TPM の中) で作った署名から暗号化の鍵を作ります。Hello を通らないと復号できません。
- 取り出したパスワードは RDP コントロール (Windows 標準の RDP ActiveX) に直接渡し、ファイルには書きません。

## いまの状態 (v0.1、作り始め)

| 機能 | 状態 |
| --- | --- |
| ホスト一覧 (追加・編集・削除・検索) | 動く |
| RDP 接続 (タブで開く、切断と再接続) | 作った。実機で確認待ち |
| 内蔵の保管庫 (Windows Hello で暗号化) | 作った。暗号化部分はテスト済み、Hello 部分は実機で確認待ち |
| Hello の確認を N 分省略する設定 | 作った (既定は毎回確認。PC のロックで省略は解除) |
| KeePassXC 連携 | 未実装 (次の段階) |
| 画面の分割表示 | 未実装 |
| 1Password / Bitwarden | 予定 (v1 のあと) |
| 証明書のピン留め、SSH トンネルなどの経路 | 作らない (経路は WireGuard などツールの外で) |

## 使い方

1. GitHub の **Actions** → **build** の最新の実行を開き、**Artifacts** の `howdy-rdp-win-x64` をダウンロードして展開します。
2. `howdy-rdp.exe` を実行します。インストールは要りません (.NET ランタイム込みの 1 ファイル)。
   - 署名していない exe なので、初回は SmartScreen の警告が出ます。
   - `SHA256SUMS.txt` でファイルが壊れていないか確かめられます。
3. 「追加」でホストを登録します。保管先が「内蔵」なら、パスワードを入れて保存すると Windows Hello の確認が出ます。

必要なもの: Windows 10 2004 以降 / Windows 11 (x64)、Windows Hello (顔・指紋・PIN のどれか)。

## データの置き場所

`%APPDATA%\howdy-rdp\`

| ファイル | 中身 |
| --- | --- |
| `hosts.json` | ホスト一覧 (パスワードは含まない) |
| `settings.json` | 設定 |
| `vault.json` | 内蔵の保管庫。ホストごとに AES-256-GCM で暗号化したパスワード |

Windows Hello の鍵は `howdy-rdp-vault-v1` という名前で、ユーザーと PC ごとに作られます。Windows の再インストールや Hello・PIN のリセットで鍵が消えると、内蔵のパスワードは戻せません (設定の「鍵を作り直す」で入れ直します)。

## 内蔵の保管庫のしくみ

1. 保管庫ごとの乱数 (salt) と決まった文字列を、Windows Hello の鍵 (`KeyCredentialManager`) で署名する。ここで Hello の確認が出る
2. 署名から HKDF-SHA256 で 256 bit の鍵を作る (RSA PKCS#1 v1.5 の署名は同じ入力なら毎回同じになる前提)
3. その鍵で、選んだホストのパスワードだけを AES-256-GCM で復号する。ホストの Id を AAD に入れ、別のホストの欄に差し替えられても復号できないようにしている
4. 鍵とパスワードはメモリから消す (省略時間を設定したときだけ、その間は鍵をメモリに置く)

Windows 標準の暗号化 (DPAPI) の前に Hello を出すだけの方法は使っていません。同じユーザーで動く別のプログラムなら Hello を通らずに復号できてしまうためです。

## 開発

```
src/HowdyRdp.Core        OS に依存しない部分 (ホスト一覧、保管庫の暗号化、保管先)。Linux でもビルドとテストができる
src/HowdyRdp.App         WinForms の画面、RDP ActiveX、Windows Hello
tests/HowdyRdp.Core.Tests
```

- .NET 10 SDK を使います。
- テスト: `dotnet test tests/HowdyRdp.Core.Tests`
- RDP ActiveX の相互運用 (`COMReference`) は Visual Studio の `MSBuild.exe` でしかビルドできません。Windows 以外で `dotnet build` すると、RDP 部分を外したものができます (`NO_RDP_COM`)。配布用の exe は GitHub Actions (`.github/workflows/build.yml`) で作ります。
- テストやサンプルに本物のホスト名・パスワード・鍵を入れないでください (`example.invalid` などを使う)。

脆弱性の報告は [SECURITY.md](SECURITY.md) を見てください。

## ライセンス

[MIT](LICENSE)
