using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AwsIpUpdate;

class TrayApplicationContext : ApplicationContext
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private string  _localIp    = "…";
    private string  _externalIp = "…";
    private string  _lastKnownIp;           // persisted across restarts

    public TrayApplicationContext()
    {
        AppPaths.EnsureDirectoryExists();

        _lastKnownIp = IpStateStore.ReadLastIp() ?? "";

        // Write a template config if none exists so the user has something to edit
        AppConfig.WriteTemplate();

        _trayIcon = new NotifyIcon
        {
            Icon             = SystemIcons.Information,
            ContextMenuStrip = BuildContextMenu(),
            Visible          = true,
            Text             = "Loading…",
        };

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _refreshTimer.Tick += async (_, _) => await RunUpdateCycleAsync();
        _refreshTimer.Start();

        _ = RunUpdateCycleAsync();
    }

    // -------------------------------------------------------------------------
    //  Core update cycle
    // -------------------------------------------------------------------------

    private async Task RunUpdateCycleAsync()
    {
        _localIp    = GetLocalIpAddress();
        _externalIp = await GetExternalIpAddressAsync();

        var config = AppConfig.TryLoad();

        if (_externalIp != "Unknown" && _externalIp != _lastKnownIp)
        {
            if (config?.IsConfigured == true)
                await TryUpdateAwsAsync(config, _externalIp);

            // Always persist the new IP so we don't re-attempt on the next tick
            _lastKnownIp = _externalIp;
            IpStateStore.WriteLastIp(_externalIp);
        }
        ApplyTooltip();
    }

    private async Task ForceUpdateAwsAsync()
    {
        _externalIp = await GetExternalIpAddressAsync();

        var config = AppConfig.TryLoad();
        if (config?.IsConfigured != true)
        {
            ApplyTooltip();
            return;
        }

        if (_externalIp == "Unknown")
        {
            _trayIcon.ShowBalloonTip(5000, "Force Update Failed",
                "Could not determine external IP.", ToolTipIcon.Warning);
            ApplyTooltip();
            return;
        }

        await TryUpdateAwsAsync(config, _externalIp);

        _lastKnownIp = _externalIp;
        IpStateStore.WriteLastIp(_externalIp);
        ApplyTooltip();
    }

    private async Task TryUpdateAwsAsync(AppConfig config, string newIp)
    {
        try
        {
            var service = new AwsSecurityGroupService(config);
            await service.UpdateAsync(newIp);

            _trayIcon.ShowBalloonTip(5000, "IP Updated",
                $"Security group updated — new IP: {newIp}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(8000, "AWS Update Failed", ex.Message, ToolTipIcon.Error);
        }
    }

    // -------------------------------------------------------------------------
    //  Tooltip
    // -------------------------------------------------------------------------

    private void ApplyTooltip()
    {
        // NotifyIcon.Text is capped at 63 characters
        string text = $"Local:  {_localIp}\nExt:    {_externalIp}";
        _trayIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    // -------------------------------------------------------------------------
    //  Context menu
    // -------------------------------------------------------------------------

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var refreshItem = new ToolStripMenuItem("Refresh Now");
        refreshItem.Click += async (_, _) => await RunUpdateCycleAsync();

        var forceItem = new ToolStripMenuItem("Force Update AWS");
        forceItem.Click += async (_, _) => await ForceUpdateAwsAsync();

        var configItem = new ToolStripMenuItem("Open Config…");
        configItem.Click += OnOpenConfig;

        menu.Items.Add(refreshItem);
        menu.Items.Add(forceItem);
        menu.Items.Add(configItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        return menu;
    }

    private static void OnOpenConfig(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo(AppPaths.ConfigFile) { UseShellExecute = true });
    }

    // -------------------------------------------------------------------------
    //  IP helpers
    // -------------------------------------------------------------------------

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                    return address.ToString();
            }
            return "Unknown";
        }
    }

    private static async Task<string> GetExternalIpAddressAsync()
    {
        string[] endpoints =
        [
            "https://checkip.amazonaws.com",
            "https://api.ipify.org",
            "https://icanhazip.com",
        ];

        foreach (string url in endpoints)
        {
            try
            {
                string result = await Http.GetStringAsync(url);
                return result.Trim();
            }
            catch { }
        }

        return "Unknown";
    }

    // -------------------------------------------------------------------------
    //  Exit
    // -------------------------------------------------------------------------

    private void OnExit(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _refreshTimer.Dispose();
        Http.Dispose();
        Application.Exit();
    }
}
