using System.Runtime.Versioning;
using System.Text;

[assembly: SupportedOSPlatform("windows")]

namespace AiProxyHub;

public sealed class MainForm : Form
{
    private readonly NotifyIcon _notifyIcon;
    private readonly TextBox _logBox;
    private bool _isExiting;

    public MainForm(int port, IServiceProvider services, Dictionary<string, string> envVars)
    {
        // Resolve services from DI
        var providerRegistry = services.GetRequiredService<ProviderRegistry>();
        var modelCatalog = services.GetRequiredService<ModelCatalogService>();

        string defaultModel = providerRegistry.DefaultModel;
        string providers = string.Join(", ", providerRegistry.Providers.Select(pv => pv.Name));
        string models = string.Join(", ", modelCatalog.AvailableModels);
        string? proxyApiKey = Environment.GetEnvironmentVariable("PROXY_API_KEY");
        string authStatus = string.IsNullOrEmpty(proxyApiKey) ? "open (no key set)" : "required (PROXY_API_KEY)";

        // Load the app icon (embedded in the exe via csproj ApplicationIcon)
        Icon appIcon = LoadAppIcon();
        Icon = appIcon;

        Text = "AI Proxy Hub";
        Width = 720;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(400, 300);

        // --- Status label ---
        var statusLabel = new Label
        {
            Text = $"AI Proxy Hub — Running on http://localhost:{port}/v1",
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Height = 36,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            Padding = new Padding(6)
        };

        // --- Log text box ---
        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(0, 200, 100),
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.None
        };

        Controls.Add(_logBox);
        Controls.Add(statusLabel);

        // --- System tray icon ---
        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = $"AI Proxy Hub — http://localhost:{port}",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _notifyIcon.DoubleClick += OnTrayDoubleClick;

        // --- Form events ---
        Resize += OnFormResize;

        // --- Initial banner ---
        AppendLog($"══════════════════════════════════════════════");
        AppendLog($"DeepSeek / Multi-Provider Copilot Proxy (Ultra)");
        AppendLog($"Version: 2026.07.20");
        AppendLog("══════════════════════════════════════════════");
        AppendLog($"Default model : {defaultModel}");
        AppendLog($"Providers     : {providers}");
        AppendLog($"Models loaded : {models}");
        AppendLog($"URL           : http://localhost:{port}/v1");
        AppendLog($"Auth          : {authStatus}");

        // --- Log environment variables (from .env) ---
        foreach (var kvp in envVars.OrderBy(e => e.Key))
        {
            string displayValue = MaskIfApiKey(kvp.Key, kvp.Value);
            AppendLog($"{kvp.Key} = {displayValue}");
        }
        AppendLog("══════════════════════════════════════════════");
    }

    /// <summary>
    /// Masks API key values: shows first 2 and last 2 characters with *** between.
    /// Non-key variables are shown in full.
    /// </summary>
    private static string MaskIfApiKey(string key, string value)
    {
        bool isApiKey = key.EndsWith("_API_KEY", StringComparison.OrdinalIgnoreCase)
                        || key.IndexOf("API_KEY", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isApiKey || value.Length <= 4)
            return value;

        return $"{value[..2]}***{value[^2..]}";
    }

    /// <summary>Loads the application icon from the output directory or exe.</summary>
    private static Icon LoadAppIcon()
    {
        // Try loading from output directory (Content copy)
        string iconPath = Path.Combine(AppContext.BaseDirectory, "proxy.ico");
        if (File.Exists(iconPath))
            return new Icon(iconPath);

        // Fallback: extract from the exe itself (ApplicationIcon)
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath)!; }
        catch { return SystemIcons.Application; }
    }

    /// <summary>Thread-safe method to append a line to the log text box.</summary>
    public void AppendLog(string message)
    {
        if (_isExiting || IsDisposed || _logBox.IsDisposed || WindowState == FormWindowState.Minimized) return;

        string line = $"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}";

        if (_logBox.InvokeRequired)
        {
            try { _logBox.BeginInvoke(() => AppendLogCore(line)); }
            catch (ObjectDisposedException) { /* tearing down */ }
        }
        else
        {
            AppendLogCore(line);
        }
    }

    private void AppendLogCore(string text)
    {
        if (_logBox.IsDisposed) return;
        _logBox.AppendText(text);
        // Auto-scroll to bottom
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    // ── System tray ──────────────────────────────────────────────

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Window", null, (_, _) => RestoreWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e) => RestoreWindow();

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            // _notifyIcon.ShowBalloonTip(
            //     2000,
            //     "AI Proxy Hub",
            //     "Application minimized to system tray.\nDouble-click to restore.",
            //     ToolTipIcon.Info);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        ExitApplication();
    }




    private async void ExitApplication()
    {
        _isExiting = true;
        _notifyIcon.Dispose();

    }
}

/// <summary>A <see cref="TextWriter"/> that redirects <see cref="Console"/> output to a <see cref="MainForm"/> log box.</summary>
public sealed class FormLogWriter : TextWriter
{
    private readonly MainForm _form;

    public FormLogWriter(MainForm form) => _form = form;
    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string? value)
    {
        if (value is not null)
            _form.AppendLog(value);
    }

    public override void Write(string? value)
    {
        if (value is not null)
            _form.AppendLog(value.TrimEnd('\r', '\n'));
    }
}
