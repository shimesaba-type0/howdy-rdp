using System.Runtime.InteropServices;

namespace HowdyRdp.App.Hello;

/// <summary>
/// デスクトップアプリから Windows Hello を呼ぶと、確認の画面がほかの窓の後ろに出ることがある。
/// しばらくのあいだ確認の画面を探し、見つかったら前に出す。
/// (KeePassXC など他のツールでも使われている回避策。効かない場合はタスクバーから確認の画面を選ぶ)
/// </summary>
internal sealed class HelloDialogFocus : IDisposable
{
    private const string DialogClassName = "Credential Dialog Xaml Host";
    private readonly CancellationTokenSource _cts = new();

    private HelloDialogFocus()
    {
        _ = Task.Run(() => PollAsync(_cts.Token));
    }

    public static HelloDialogFocus Start() => new();

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private static async Task PollAsync(CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var hwnd = FindWindowW(DialogClassName, null);
            if (hwnd != IntPtr.Zero)
            {
                SetForegroundWindow(hwnd);
                return;
            }

            try
            {
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
