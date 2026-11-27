using HowdyRdp.App.Rdp;
using HowdyRdp.Core.Credentials;
using HowdyRdp.Core.Models;
using HowdyRdp.Core.Storage;
using HowdyRdp.Core.Vault;
using Microsoft.Win32;

namespace HowdyRdp.App.Forms;

/// <summary>メイン画面。最初のタブがホスト一覧、接続したセッションは右にタブで並ぶ。</summary>
internal sealed class MainForm : Form
{
    private readonly HostRepository _hosts;
    private readonly SettingsRepository _settings;
    private readonly BuiltInVault _vault;
    private readonly CredentialSourceRegistry _sources;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _listPage = new("ホスト一覧");
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
    };
    private readonly ToolStripTextBox _search = new() { Width = 200 };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ContextMenuStrip _tabMenu = new();
    private bool _busy;

    public MainForm(HostRepository hosts, SettingsRepository settings, BuiltInVault vault, CredentialSourceRegistry sources)
    {
        _hosts = hosts;
        _settings = settings;
        _vault = vault;
        _sources = sources;

        Text = Ui.AppName;
        Size = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildListPage();
        _tabs.TabPages.Add(_listPage);
        _tabs.MouseUp += OnTabsMouseUp;
        _tabMenu.Items.Add("タブを閉じる", null, (_, _) => CloseTab(_tabs.SelectedTab));

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(_tabs);
        Controls.Add(statusStrip);

        RefreshList();
        UpdateStatus();

        // PC をロックしたら、Hello の省略に使っている鍵をメモリから消す
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var active = Sessions().Count(s => s.IsConnected);
        if (active > 0 && !Ui.Confirm(this, $"接続中のセッションが {active} 件あります。切断して終了しますか?"))
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        foreach (var session in Sessions().ToList())
        {
            session.Dispose();
        }

        _vault.ClearCache();
        base.OnFormClosed(e);
    }

    private void BuildListPage()
    {
        _list.Columns.Add("名前", 200);
        _list.Columns.Add("ホスト", 240);
        _list.Columns.Add("ユーザー", 180);
        _list.Columns.Add("保管先", 110);
        _list.Columns.Add("最終接続", 140);
        _list.ItemActivate += async (_, _) => await ConnectSelectedAsync();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
            }
        };

        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 4, 6, 4) };
        tools.Items.Add(new ToolStripButton("接続", null, async (_, _) => await ConnectSelectedAsync()) { Font = new Font(Font, FontStyle.Bold) });
        tools.Items.Add(new ToolStripButton("追加", null, async (_, _) => await AddHostAsync()));
        tools.Items.Add(new ToolStripButton("編集", null, async (_, _) => await EditSelectedAsync()));
        tools.Items.Add(new ToolStripButton("削除", null, (_, _) => DeleteSelected()));
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(new ToolStripButton("設定", null, async (_, _) => await OpenSettingsAsync()));
        tools.Items.Add(new ToolStripLabel("検索") { Alignment = ToolStripItemAlignment.Right });
        _search.Alignment = ToolStripItemAlignment.Right;
        _search.TextChanged += (_, _) => RefreshList();
        tools.Items.Insert(tools.Items.Count - 1, _search);

        _listPage.Controls.Add(_list);
        _listPage.Controls.Add(tools);
    }

    private void RefreshList()
    {
        var selectedId = SelectedHost()?.Id;
        var query = _search.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var host in _hosts.All.OrderBy(h => h.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            if (query.Length > 0
                && !host.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                && !host.HostName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            var item = new ListViewItem(host.Title) { Tag = host };
            item.SubItems.Add(host.Port == 3389 ? host.HostName : $"{host.HostName}:{host.Port}");
            item.SubItems.Add(host.UserName);
            item.SubItems.Add(CredentialSourceRegistry.DisplayNameOf(host.Source));
            item.SubItems.Add(host.LastConnected?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "");
            item.Selected = host.Id == selectedId;
            _list.Items.Add(item);
        }

        if (_list.SelectedItems.Count == 0 && _list.Items.Count > 0)
        {
            _list.Items[0].Selected = true;
        }

        _list.EndUpdate();
    }

    private void UpdateStatus(string? message = null)
    {
        var minutes = _settings.Current.HelloSkipMinutes;
        var hello = minutes == 0 ? "本人確認: 接続のたびに" : $"本人確認: {minutes} 分は省略";
        var baseText = $"内蔵: Hello 鍵で暗号化 ({_vault.Count} 件)   KeePassXC: 未実装   {hello}";
        _status.Text = message is null ? baseText : $"{message}   |   {baseText}";
    }

    private HostEntry? SelectedHost() =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as HostEntry : null;

    private IEnumerable<RdpSessionControl> Sessions() =>
        _tabs.TabPages.Cast<TabPage>().SelectMany(p => p.Controls.OfType<RdpSessionControl>());

    private async Task AddHostAsync()
    {
        using var form = new HostEditForm(null, hasStoredPassword: false);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _hosts.Upsert(form.Host);
        await SavePasswordIfGivenAsync(form.Host, form.NewPassword);
        RefreshList();
        UpdateStatus();
    }

    private async Task EditSelectedAsync()
    {
        var host = SelectedHost();
        if (host is null)
        {
            return;
        }

        using var form = new HostEditForm(host, _vault.HasPassword(host.Id));
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var edited = form.Host;
        _hosts.Upsert(edited);
        if (edited.Source != CredentialSourceKind.BuiltIn && _vault.HasPassword(edited.Id))
        {
            // 保管先を内蔵から変えたら、内蔵の分は消す
            _vault.Remove(edited.Id);
        }

        await SavePasswordIfGivenAsync(edited, form.NewPassword);
        RefreshList();
        UpdateStatus();
    }

    private void DeleteSelected()
    {
        var host = SelectedHost();
        if (host is null || !Ui.Confirm(this, $"「{host.Title}」を削除しますか? 内蔵に保存したパスワードも消えます。", MessageBoxIcon.Warning))
        {
            return;
        }

        _hosts.Remove(host.Id);
        _vault.Remove(host.Id);
        RefreshList();
        UpdateStatus();
    }

    private async Task OpenSettingsAsync()
    {
        using var form = new SettingsForm(_settings.Current, _vault.Count);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (form.ResetRequested)
        {
            await ResetVaultAsync();
            return;
        }

        _settings.Save(new AppSettings { HelloSkipMinutes = form.HelloSkipMinutes });
        _vault.SkipDuration = TimeSpan.FromMinutes(_settings.Current.HelloSkipMinutes);
        _vault.ClearCache();
        UpdateStatus();
    }

    private async Task ResetVaultAsync()
    {
        if (!Ui.Confirm(
                this,
                $"内蔵の鍵を作り直します。内蔵に保存したパスワード ({_vault.Count} 件) はすべて消え、入れ直しになります。続けますか?",
                MessageBoxIcon.Warning))
        {
            return;
        }

        await RunVaultOperationAsync(() => _vault.ResetAsync(), "鍵を作り直しました。");
        UpdateStatus();
    }

    /// <summary>内蔵にパスワードを保存する。初回は鍵を作る前に説明を出す。</summary>
    private async Task<bool> SavePasswordIfGivenAsync(HostEntry host, string password)
    {
        if (host.Source != CredentialSourceKind.BuiltIn || password.Length == 0)
        {
            return true;
        }

        if (!_vault.IsInitialized && !ConfirmFirstKey())
        {
            return false;
        }

        return await RunVaultOperationAsync(() => _vault.SetPasswordAsync(host.Id, password), "パスワードを暗号化して保存しました。");
    }

    private bool ConfirmFirstKey() => Ui.Confirm(
        this,
        "はじめて内蔵に保存するので、このPC専用の鍵を作ります。\n\n" +
        "・Windows Hello の確認が 2 回出ます (鍵の作成と、保存のための確認)。\n" +
        "・鍵は TPM の中に作られ、PC の外には出せません。\n" +
        "・Windows の再インストールや Hello・PIN のリセットで鍵は消え、内蔵のパスワードは戻せません。元のパスワードは別の場所にも控えておいてください。",
        MessageBoxIcon.Information);

    private async Task<bool> RunVaultOperationAsync(Func<Task> operation, string doneMessage)
    {
        try
        {
            UseWaitCursor = true;
            await operation();
            UpdateStatus(doneMessage);
            return true;
        }
        catch (VaultKeyException ex) when (ex.Error == VaultKeyError.Cancelled)
        {
            UpdateStatus("本人確認がキャンセルされたので、保存していません。");
            return false;
        }
        catch (VaultKeyException ex)
        {
            Ui.Error(this, ex.Message);
            return false;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task ConnectSelectedAsync()
    {
        var host = SelectedHost();
        if (host is not null)
        {
            await ConnectAsync(host, existing: null);
        }
    }

    /// <summary>パスワードを取り出して接続する。existing を渡すと、そのタブで再接続する。</summary>
    private async Task ConnectAsync(HostEntry host, RdpSessionControl? existing)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        string? password = null;
        try
        {
            UpdateStatus($"{host.Title}: パスワードを取り出しています…");
            var result = await _sources.Get(host.Source).GetPasswordAsync(host);

            switch (result.Status)
            {
                case CredentialStatus.Ok:
                    password = result.Password;
                    break;

                case CredentialStatus.NotFound when host.Source == CredentialSourceKind.BuiltIn:
                    password = AskPassword(host, out var save);
                    if (password is null)
                    {
                        UpdateStatus();
                        return;
                    }

                    if (save)
                    {
                        // 保存に失敗しても (Hello のキャンセルなど)、入力されたパスワードで接続は続ける
                        if (_vault.IsInitialized || ConfirmFirstKey())
                        {
                            await RunVaultOperationAsync(() => _vault.SetPasswordAsync(host.Id, password), "パスワードを暗号化して保存しました。");
                        }
                    }

                    break;

                case CredentialStatus.Cancelled:
                    UpdateStatus("本人確認がキャンセルされました。パスワードは取り出していません。");
                    return;

                default:
                    Ui.Error(this, result.Message ?? "パスワードを取り出せませんでした。");
                    UpdateStatus();
                    return;
            }

            if (password is null)
            {
                return;
            }

            OpenSession(host, password, existing);
            host.LastConnected = DateTimeOffset.Now;
            _hosts.Upsert(host);
            RefreshList();
            UpdateStatus($"{host.Title} に接続しています…");
        }
        finally
        {
            password = null;
            _busy = false;
        }
    }

    private string? AskPassword(HostEntry host, out bool save)
    {
        using var form = new PasswordPromptForm(host.Title, host.UserName);
        var ok = form.ShowDialog(this) == DialogResult.OK;
        save = ok && form.SaveToVault;
        return ok ? form.Password : null;
    }

    private void OpenSession(HostEntry host, string password, RdpSessionControl? existing)
    {
        var session = existing;
        if (session is null)
        {
            var page = new TabPage(host.Title) { ToolTipText = host.HostName };
            session = new RdpSessionControl(host);
            session.LoggedIn += (_, _) => UpdateStatus($"{host.Title} にログオンしました。");
            session.Disconnected += (_, message) =>
            {
                page.Text = host.Title + " (切断)";
                UpdateStatus($"{host.Title}: {message.Replace('\n', ' ')}");
            };
            session.ReconnectRequested += async (s, _) =>
            {
                page.Text = host.Title;
                await ConnectAsync(host, (RdpSessionControl)s!);
            };
            page.Controls.Add(session);
            _tabs.TabPages.Add(page);
            _tabs.SelectedTab = page;
        }

        try
        {
            session.Connect(password);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or InvalidOperationException)
        {
            Ui.Error(this, $"RDP コントロールを準備できませんでした。\n{ex.Message}");
        }
    }

    private void OnTabsMouseUp(object? sender, MouseEventArgs e)
    {
        for (var i = 1; i < _tabs.TabCount; i++)
        {
            if (!_tabs.GetTabRect(i).Contains(e.Location))
            {
                continue;
            }

            if (e.Button == MouseButtons.Middle)
            {
                CloseTab(_tabs.TabPages[i]);
            }
            else if (e.Button == MouseButtons.Right)
            {
                _tabs.SelectedIndex = i;
                _tabMenu.Show(_tabs, e.Location);
            }

            return;
        }
    }

    private void CloseTab(TabPage? page)
    {
        if (page is null || page == _listPage)
        {
            return;
        }

        var session = page.Controls.OfType<RdpSessionControl>().FirstOrDefault();
        if (session is { IsConnected: true } && !Ui.Confirm(this, $"「{page.Text}」を切断してタブを閉じますか?"))
        {
            return;
        }

        _tabs.TabPages.Remove(page);
        page.Dispose();
        _tabs.SelectedTab = _listPage;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
        {
            _vault.ClearCache();
        }
    }
}
