# セキュリティ方針 / Security Policy

howdy-rdp は RDP のパスワードを扱うツールです。脆弱性を見つけたら、公開の Issue には書かず、非公開で知らせてください。

howdy-rdp handles RDP credentials. Please report vulnerabilities privately, not in public issues.

## 報告のしかた / How to report

GitHub の **Security** タブ → **Report a vulnerability** から送ってください (GitHub の非公開の脆弱性報告)。

Use **Security → Report a vulnerability** on this repository (GitHub private vulnerability reporting).

書いてほしいこと / Please include:

- 影響を受けるバージョンやコミット / affected version or commit
- 再現手順 / steps to reproduce
- 想定される影響 (例: Hello を通らずにパスワードを取り出せる) / impact (e.g. reading a stored password without Windows Hello)

報告に本物のホスト名・パスワード・鍵を含めないでください。 / Do not include real hostnames, passwords or keys.

個人で開発しているため、返事には数日かかることがあります。 / This is a personal project; replies may take a few days.

## 対象 / Scope

特に知りたいもの / Of particular interest:

- Windows Hello の確認を通らずに、内蔵の保管庫 (`%APPDATA%\howdy-rdp\vault.json`) からパスワードを取り出せる
- パスワードがファイル、ログ、Windows の資格情報マネージャーなどに平文で残る
- KeePassXC など外部の保管先との連携で、意図しない相手にパスワードが渡る
- 配布している exe やビルドの手順 (GitHub Actions) の問題

対象外 / Out of scope:

- 管理者権限を持つ攻撃者、または PC を物理的に分解できる攻撃者 / attackers with admin rights or physical access to the hardware
- 同じユーザーで動くマルウェアが、接続中の RDP セッションのメモリを読むこと (接続に使うパスワードは一時的にメモリに載るため) / malware reading process memory during an active session
- Windows Hello、TPM、RDP プロトコルそのものの脆弱性 (それぞれ Microsoft に報告してください)

## 対応しているバージョン / Supported versions

最新の main ブランチとそこから作ったビルドだけを直します。 / Only the latest `main` is supported.
