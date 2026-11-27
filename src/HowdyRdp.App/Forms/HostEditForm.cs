using HowdyRdp.Core.Credentials;
using HowdyRdp.Core.Models;

namespace HowdyRdp.App.Forms;

/// <summary>ホストの追加・編集。保管先が内蔵ならパスワード欄、KeePassXC なら照合する URL を出す。</summary>
internal sealed class HostEditForm : Form
{
    private readonly TextBox _displayName = new() { Width = 340 };
    private readonly TextBox _hostName = new() { Width = 250 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Value = 3389, Width = 80 };
    private readonly TextBox _userName = new() { Width = 340, PlaceholderText = @"DOMAIN\user または user" };
    private readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _password = new() { Width = 340, UseSystemPasswordChar = true };
    private readonly CheckBox _showPassword = new() { Text = "表示", AutoSize = true };
    private readonly Panel _builtInPanel = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Panel _keePassPanel = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Label _keePassUrl = new() { AutoSize = true };
    private readonly Label _passwordNote;

    public HostEditForm(HostEntry? existing, bool hasStoredPassword)
    {
        Host = existing?.Clone() ?? new HostEntry();
        IsNew = existing is null;
        Ui.SetupDialog(this, IsNew ? "ホストの追加" : "ホストの編集");

        foreach (var kind in Enum.GetValues<CredentialSourceKind>())
        {
            _source.Items.Add(new SourceItem(kind));
        }

        _displayName.Text = Host.DisplayName;
        _hostName.Text = Host.HostName;
        _port.Value = Math.Clamp(Host.Port, 1, 65535);
        _userName.Text = Host.UserName;
        _source.SelectedIndex = (int)Host.Source;

        _password.PlaceholderText = hasStoredPassword ? "保存済み (変えるときだけ入力)" : "";
        _passwordNote = Ui.Note(hasStoredPassword
            ? "入力して「保存」を押すと Windows Hello で確認し、暗号化して保存し直します。空欄なら今のパスワードのままです。"
            : "「保存」を押すと Windows Hello で確認し、暗号化してから保存します。空欄なら接続するときに聞きます。");

        _showPassword.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !_showPassword.Checked;
        _hostName.TextChanged += (_, _) => UpdateKeePassUrl();
        _source.SelectedIndexChanged += (_, _) => UpdateSourcePanels();

        var layout = Ui.Stack();
        layout.Controls.Add(Ui.Caption("表示名"));
        layout.Controls.Add(_displayName);

        var hostRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
        var hostCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty };
        hostCol.Controls.Add(Ui.Caption("ホスト名 (IP アドレスも可)"));
        hostCol.Controls.Add(_hostName);
        var portCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty };
        portCol.Controls.Add(Ui.Caption("ポート"));
        portCol.Controls.Add(_port);
        hostRow.Controls.Add(hostCol);
        hostRow.Controls.Add(portCol);
        layout.Controls.Add(hostRow);

        layout.Controls.Add(Ui.Caption("ユーザー名"));
        layout.Controls.Add(_userName);
        layout.Controls.Add(Ui.Caption("パスワードの保管先"));
        layout.Controls.Add(_source);

        var builtIn = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty, WrapContents = false };
        builtIn.Controls.Add(Ui.Caption("パスワード"));
        var pwRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
        pwRow.Controls.Add(_password);
        pwRow.Controls.Add(_showPassword);
        builtIn.Controls.Add(pwRow);
        builtIn.Controls.Add(_passwordNote);
        _builtInPanel.Controls.Add(builtIn);

        var keePass = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty, WrapContents = false };
        keePass.Controls.Add(Ui.Caption("KeePassXC のエントリ (URL で照合)"));
        keePass.Controls.Add(_keePassUrl);
        keePass.Controls.Add(Ui.Note("KeePassXC 側で、このホストのエントリの URL をこの値にしておきます。KeePassXC との連携はまだ作っていません (次の段階)。"));
        _keePassPanel.Controls.Add(keePass);

        layout.Controls.Add(_builtInPanel);
        layout.Controls.Add(_keePassPanel);

        Controls.Add(layout);
        Controls.Add(Ui.DialogButtons(this));

        UpdateKeePassUrl();
        UpdateSourcePanels();
    }

    public HostEntry Host { get; }

    public bool IsNew { get; }

    /// <summary>入力されたパスワード。空なら変更なし。</summary>
    public string NewPassword => _password.Text;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && !ValidateAndApply())
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    private bool ValidateAndApply()
    {
        var host = _hostName.Text.Trim();
        if (host.Length == 0)
        {
            Ui.Error(this, "ホスト名を入れてください。");
            _hostName.Focus();
            return false;
        }

        if (host.Any(char.IsWhiteSpace) || host.Contains("://", StringComparison.Ordinal))
        {
            Ui.Error(this, "ホスト名には空白や「rdp://」などを入れないでください。");
            _hostName.Focus();
            return false;
        }

        Host.DisplayName = _displayName.Text.Trim();
        Host.HostName = host;
        Host.Port = (int)_port.Value;
        Host.UserName = _userName.Text.Trim();
        Host.Source = ((SourceItem)_source.SelectedItem!).Kind;
        return true;
    }

    private void UpdateKeePassUrl() => _keePassUrl.Text = $"rdp://{_hostName.Text.Trim()}";

    private void UpdateSourcePanels()
    {
        var kind = ((SourceItem?)_source.SelectedItem)?.Kind ?? CredentialSourceKind.BuiltIn;
        _builtInPanel.Visible = kind == CredentialSourceKind.BuiltIn;
        _keePassPanel.Visible = kind == CredentialSourceKind.KeePassXc;
    }

    private sealed record SourceItem(CredentialSourceKind Kind)
    {
        public override string ToString() => CredentialSourceRegistry.DisplayNameOf(Kind);
    }
}
