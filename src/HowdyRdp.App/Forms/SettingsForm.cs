using HowdyRdp.Core.Models;

namespace HowdyRdp.App.Forms;

/// <summary>設定。Hello の省略時間と、内蔵の鍵の作り直し。</summary>
internal sealed class SettingsForm : Form
{
    private readonly RadioButton _everyTime = new() { Text = "接続のたびに確認する", AutoSize = true };
    private readonly RadioButton _skip = new() { Text = "一度確認したら", AutoSize = true };
    private readonly ComboBox _minutes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };

    public SettingsForm(AppSettings current, int builtInCount)
    {
        Ui.SetupDialog(this, "設定");

        foreach (var m in AppSettings.AllowedHelloSkipMinutes.Where(m => m > 0))
        {
            _minutes.Items.Add(m);
        }

        _everyTime.Checked = current.HelloSkipMinutes == 0;
        _skip.Checked = current.HelloSkipMinutes > 0;
        _minutes.SelectedItem = current.HelloSkipMinutes > 0 ? current.HelloSkipMinutes : 5;
        _minutes.Enabled = _skip.Checked;
        _skip.CheckedChanged += (_, _) => _minutes.Enabled = _skip.Checked;

        var layout = Ui.Stack();
        layout.Controls.Add(Ui.Caption("本人確認 (Windows Hello)"));
        layout.Controls.Add(_everyTime);
        var skipRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
        skipRow.Controls.Add(_skip);
        skipRow.Controls.Add(_minutes);
        skipRow.Controls.Add(new Label { Text = "分は省略する", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
        layout.Controls.Add(skipRow);
        layout.Controls.Add(Ui.Note("省略中でも、ツールを閉じるか PC をロックしたら、次は必ず Hello を出します。"));

        layout.Controls.Add(Ui.Caption("内蔵の保管先"));
        layout.Controls.Add(new Label { Text = $"保存済み {builtInCount} 件", AutoSize = true });
        var reset = new Button { Text = "鍵を作り直す…", AutoSize = true };
        reset.Click += (_, _) =>
        {
            ResetRequested = true;
            DialogResult = DialogResult.OK;
        };
        layout.Controls.Add(reset);
        layout.Controls.Add(Ui.Note("Windows の再インストールや Hello・PIN のリセットで鍵が消えたときに使います。内蔵に保存したパスワードはすべて消え、入れ直しになります。"));

        Controls.Add(layout);
        Controls.Add(Ui.DialogButtons(this));
    }

    public bool ResetRequested { get; private set; }

    public int HelloSkipMinutes => _skip.Checked && _minutes.SelectedItem is int m ? m : 0;
}
