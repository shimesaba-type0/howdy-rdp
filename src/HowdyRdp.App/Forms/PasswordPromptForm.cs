namespace HowdyRdp.App.Forms;

/// <summary>内蔵にパスワードがまだないとき、接続の前に聞く。</summary>
internal sealed class PasswordPromptForm : Form
{
    private readonly TextBox _password = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly CheckBox _save = new() { Text = "内蔵に保存する (Windows Hello で確認して暗号化)", AutoSize = true, Checked = true };

    public PasswordPromptForm(string hostTitle, string userName)
    {
        Ui.SetupDialog(this, "パスワードの入力");
        var layout = Ui.Stack();
        layout.Controls.Add(new Label { Text = $"{hostTitle} のパスワードはまだ保存されていません。", AutoSize = true });
        layout.Controls.Add(Ui.Caption($"ユーザー: {userName}"));
        layout.Controls.Add(Ui.Caption("パスワード"));
        layout.Controls.Add(_password);
        layout.Controls.Add(_save);
        Controls.Add(layout);
        Controls.Add(Ui.DialogButtons(this, "接続"));
    }

    public string Password => _password.Text;

    public bool SaveToVault => _save.Checked;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && _password.TextLength == 0)
        {
            Ui.Error(this, "パスワードを入れてください。");
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }
}
