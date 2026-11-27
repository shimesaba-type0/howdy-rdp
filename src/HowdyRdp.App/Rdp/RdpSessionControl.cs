using HowdyRdp.Core.Models;
#if !NO_RDP_COM
using AxMSTSCLib;
using MSTSCLib;
#endif

namespace HowdyRdp.App.Rdp;

/// <summary>
/// 1 つの RDP セッション。Windows 標準の RDP ActiveX (MsRdpClient9NotSafeForScripting) を埋め込む。
/// パスワードはコントロールに直接渡し、Windows の資格情報マネージャーには書かない。
/// </summary>
internal sealed class RdpSessionControl : UserControl
{
    private readonly Label _overlay;
    private readonly Button _reconnectButton;
    private readonly System.Windows.Forms.Timer _resizeTimer;

#if !NO_RDP_COM
    private AxMsRdpClient9NotSafeForScripting? _rdp;
#endif

    public RdpSessionControl(HostEntry host)
    {
        Host = host;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(32, 32, 36);

        _overlay = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11f),
            Visible = false,
        };
        _reconnectButton = new Button
        {
            Text = "再接続",
            AutoSize = true,
            Visible = false,
            BackColor = SystemColors.Control,
        };
        _reconnectButton.Click += (_, _) => ReconnectRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_reconnectButton);
        Controls.Add(_overlay);

        _resizeTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer.Stop();
            UpdateRemoteSize();
        };
    }

    public HostEntry Host { get; }

    public bool IsConnected { get; private set; }

    /// <summary>ログオンまで終わった。</summary>
    public event EventHandler? LoggedIn;

    /// <summary>切断された。引数は画面に出す理由。</summary>
    public event EventHandler<string>? Disconnected;

    /// <summary>切断後に「再接続」が押された。パスワードは取り出し直す。</summary>
    public event EventHandler? ReconnectRequested;

    public void Connect(string password)
    {
        HideOverlay();
#if NO_RDP_COM
        ShowOverlay("このビルドには RDP コントロールが入っていません (Windows 以外でビルドしたため)。", showReconnect: false);
#else
        DisposeRdp();

        var rdp = new AxMsRdpClient9NotSafeForScripting { Dock = DockStyle.Fill };
        Controls.Add(rdp);
        rdp.BringToFront();
        rdp.CreateControl();
        _rdp = rdp;

        rdp.OnLoginComplete += (_, _) =>
        {
            IsConnected = true;
            LoggedIn?.Invoke(this, EventArgs.Empty);
        };
        rdp.OnDisconnected += (_, e) => OnRdpDisconnected(e.discReason);
        rdp.OnFatalError += (_, e) => OnRdpDisconnected(-1, $"RDP コントロールで致命的なエラーが起きました (コード {e.errorCode})。");

        rdp.Server = Host.HostName;
        var (domain, user) = Host.SplitUserName();
        rdp.UserName = user;
        if (!string.IsNullOrEmpty(domain))
        {
            rdp.Domain = domain;
        }

        var size = RemoteSize();
        rdp.DesktopWidth = size.Width;
        rdp.DesktopHeight = size.Height;
        rdp.ColorDepth = 32;

        var advanced = rdp.AdvancedSettings9;
        advanced.RDPPort = Host.Port;
        advanced.EnableCredSspSupport = true; // NLA
        advanced.AuthenticationLevel = 2;     // サーバーの認証に失敗したら警告を出す (mstsc の既定と同じ)
        advanced.RedirectClipboard = true;
        advanced.SmartSizing = false;

        var ocx = rdp.GetOcx() ?? throw new InvalidOperationException("RDP コントロールを作れませんでした。");

        // 資格情報の保存と、コントロール自身のパスワード入力画面を出さない
        var nonScriptable4 = (IMsRdpClientNonScriptable4)ocx;
        nonScriptable4.AllowCredentialSaving = false;
        nonScriptable4.PromptForCredentials = false;

        // パスワードはここでコントロールに渡すだけ
        ((IMsTscNonScriptable)ocx).ClearTextPassword = password;

        rdp.Connect();
#endif
    }

    public void Disconnect()
    {
#if !NO_RDP_COM
        if (_rdp is { Connected: not 0 })
        {
            try
            {
                _rdp.Disconnect();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        }
#endif
        IsConnected = false;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _reconnectButton.Location = new Point((Width - _reconnectButton.Width) / 2, (Height / 2) + 40);
        if (IsConnected)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resizeTimer.Dispose();
            Disconnect();
#if !NO_RDP_COM
            DisposeRdp();
#endif
        }

        base.Dispose(disposing);
    }

    private Size RemoteSize()
    {
        // RDP の画面サイズは 200〜8192。幅は 4 の倍数にしておく
        var w = Math.Clamp(ClientSize.Width, 640, 8192) & ~3;
        var h = Math.Clamp(ClientSize.Height, 480, 8192);
        return new Size(w, h);
    }

    private void UpdateRemoteSize()
    {
#if !NO_RDP_COM
        if (_rdp is null || !IsConnected)
        {
            return;
        }

        var size = RemoteSize();
        try
        {
            // 接続先が対応していれば、窓の大きさに合わせて解像度を変える (Windows 8.1 / Server 2012 R2 以降)
            _rdp.UpdateSessionDisplaySettings((uint)size.Width, (uint)size.Height, (uint)size.Width, (uint)size.Height, 0, 100, 100);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException)
        {
            // 対応していない接続先では何もしない
        }
#endif
    }

#if !NO_RDP_COM
    private void OnRdpDisconnected(int reason, string? message = null)
    {
        var wasConnected = IsConnected;
        IsConnected = false;

        if (message is null)
        {
            message = DescribeDisconnect(reason, wasConnected);
        }

        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() =>
        {
            DisposeRdp();
            ShowOverlay(message, showReconnect: true);
            Disconnected?.Invoke(this, message);
        });
    }

    private string DescribeDisconnect(int reason, bool wasConnected)
    {
        // 1: こちらから切断、2: 接続先でログオフや切断、3: 接続先から切断
        if (reason is 1 or 2 or 3 && wasConnected)
        {
            return "切断しました。";
        }

        string? detail = null;
        try
        {
            if (_rdp is not null)
            {
                detail = _rdp.GetErrorDescription((uint)reason, (uint)_rdp.ExtendedDisconnectReason);
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException)
        {
        }

        return string.IsNullOrWhiteSpace(detail)
            ? $"接続できませんでした (理由コード {reason})。"
            : $"接続できませんでした。\n{detail}";
    }

    private void DisposeRdp()
    {
        if (_rdp is null)
        {
            return;
        }

        var rdp = _rdp;
        _rdp = null;
        Controls.Remove(rdp);
        try
        {
            rdp.Dispose();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }
#endif

    private void ShowOverlay(string message, bool showReconnect)
    {
        _overlay.Text = message;
        _overlay.Visible = true;
        _overlay.BringToFront();
        _reconnectButton.Visible = showReconnect;
        _reconnectButton.BringToFront();
        OnResize(EventArgs.Empty);
    }

    private void HideOverlay()
    {
        _overlay.Visible = false;
        _reconnectButton.Visible = false;
    }
}
