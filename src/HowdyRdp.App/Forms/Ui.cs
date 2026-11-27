namespace HowdyRdp.App.Forms;

/// <summary>画面づくりの小さな共通部品。</summary>
internal static class Ui
{
    public const string AppName = "howdy-rdp";

    public static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 8, 0, 2),
    };

    public static Label Note(string text, int width = 360) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(width, 0),
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 4, 0, 4),
    };

    public static void Error(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static void Info(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(IWin32Window? owner, string message, MessageBoxIcon icon = MessageBoxIcon.Question) =>
        MessageBox.Show(owner, message, AppName, MessageBoxButtons.OKCancel, icon, MessageBoxDefaultButton.Button2) == DialogResult.OK;

    /// <summary>OK / キャンセルのボタン列を作り、フォームの既定ボタンにする。</summary>
    public static FlowLayoutPanel DialogButtons(Form form, string okText = "保存")
    {
        var ok = new Button { Text = okText, DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(88, 0) };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(88, 0) };
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(8),
        };
        panel.Controls.Add(ok);
        panel.Controls.Add(cancel);
        return panel;
    }

    public static void SetupDialog(Form form, string title)
    {
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }

    public static TableLayoutPanel Stack() => new()
    {
        ColumnCount = 1,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Fill,
        Padding = new Padding(16, 12, 16, 4),
    };
}
