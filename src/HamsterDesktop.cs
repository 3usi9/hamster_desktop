using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: AssemblyTitle("仓鼠小挂件")]
[assembly: AssemblyDescription("aBowman 原版仓鼠的离线桌面小挂件")]
[assembly: AssemblyVersion("1.0.0.0")]

internal static class Program
{
    internal static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HamsterDesktop");
    internal static string Payload;
    internal static string QaDirectory;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool SetDllDirectory(string path);

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && args[0] == "--self-test") QaDirectory = Path.GetFullPath(args[1]);
        bool first;
        using (var mutex = new Mutex(true, QaDirectory == null ? "Local\\ABowman.HamsterDesktop" : "Local\\ABowman.HamsterDesktop.QA", out first))
        {
            if (!first)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    IntPtr handle = FindWindow(null, "仓鼠小窝");
                    if (handle != IntPtr.Zero) { PostMessage(handle, 0x8001, IntPtr.Zero, IntPtr.Zero); break; }
                    Thread.Sleep(100);
                }
                return;
            }
            try
            {
                Payload = ExtractPayload();
                AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
                {
                    string file = Path.Combine(Payload, new AssemblyName(e.Name).Name + ".dll");
                    return File.Exists(file) ? Assembly.LoadFrom(file) : null;
                };
                SetDllDirectory(Payload);
                RunWidget();
            }
            catch (Exception error)
            {
                Log(error.ToString());
                MessageBox.Show("小挂件暂时无法启动。\n\n" + error.Message + "\n\n错误记录：" + Path.Combine(DataDirectory, "error.log"), "仓鼠小挂件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.ExitCode = 1;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunWidget() { Application.Run(new HamsterWindow()); }

    private static string ExtractPayload()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string hash;
        using (var stream = assembly.GetManifestResourceStream("hamster.bundle.zip"))
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").Substring(0, 16);
        string root = Path.Combine(DataDirectory, "runtime", hash);
        if (File.Exists(Path.Combine(root, ".complete")) && File.Exists(Path.Combine(root, "assets", "index.html")) && File.Exists(Path.Combine(root, "WebView2Loader.dll"))) return root;
        Directory.CreateDirectory(root);
        using (var stream = assembly.GetManifestResourceStream("hamster.bundle.zip"))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("资源文件路径无效。");
                if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var source = entry.Open()) using (var destination = File.Create(target)) source.CopyTo(destination);
            }
        }
        File.WriteAllText(Path.Combine(root, ".complete"), hash);
        return root;
    }

    internal static void Log(string message)
    {
        try { Directory.CreateDirectory(DataDirectory); File.AppendAllText(Path.Combine(DataDirectory, "error.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { }
    }
}

internal sealed class Preferences
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int StageWidth { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool Positioned { get; set; }
    public Preferences() { StageWidth = 360; AlwaysOnTop = true; }
}

internal sealed class HamsterWindow : Form
{
    private const string LocalOrigin = "https://hamster.local/";
    private readonly WebView2 view = new WebView2();
    private readonly Panel header = new Panel();
    private readonly Label title = new Label();
    private readonly Label footer = new Label();
    private readonly Button pin = new Button();
    private readonly Button more = new Button();
    private readonly Button hide = new Button();
    private readonly Button exit = new Button();
    private readonly ContextMenuStrip menu = new ContextMenuStrip();
    private readonly NotifyIcon tray = new NotifyIcon();
    private readonly ToolTip tips = new ToolTip();
    private readonly JavaScriptSerializer json = new JavaScriptSerializer();
    private readonly Preferences preferences;
    private readonly float scale;
    private ToolStripMenuItem topItem;
    private ToolStripMenuItem pauseItem;
    private bool paused;
    private bool ready;
    private bool testing;
    private bool failed;
    private readonly System.Windows.Forms.Timer timeout = new System.Windows.Forms.Timer();

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    internal HamsterWindow()
    {
        using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) scale = graphics.DpiX / 96f;
        preferences = LoadPreferences();
        AutoScaleMode = AutoScaleMode.None;
        Text = "仓鼠小窝";
        Font = new Font("Microsoft YaHei UI", 9f);
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.White;
        Padding = new Padding(1);
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        Icon = new Icon(Path.Combine(Program.Payload, "hamster.ico"));
        TopMost = preferences.AlwaysOnTop;

        header.BackColor = Color.FromArgb(249, 245, 238);
        title.Text = "仓鼠小窝";
        title.ForeColor = Color.FromArgb(94, 74, 51);
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Cursor = Cursors.SizeAll;
        title.Padding = new Padding(S(12), 0, 0, 0);
        header.Cursor = Cursors.SizeAll;
        header.MouseDown += DragWindow;
        title.MouseDown += DragWindow;
        title.DoubleClick += delegate { ToggleTop(); };
        header.ContextMenuStrip = menu;
        title.ContextMenuStrip = menu;
        CreateButton(pin, "置顶", "切换置顶", delegate { ToggleTop(); });
        CreateButton(more, "···", "菜单：大小、暂停、重新加载", delegate { menu.Show(more, new Point(0, more.Height)); });
        CreateButton(hide, "−", "收起到托盘；双击托盘图标恢复", delegate { HideWidget(); });
        CreateButton(exit, "×", "退出小挂件", delegate { Close(); });
        header.Controls.AddRange(new Control[] { title, pin, more, hide, exit });

        footer.Text = "单击空白处投喂 · 点击滚轮中央让它运动";
        footer.TextAlign = ContentAlignment.MiddleCenter;
        footer.ForeColor = Color.FromArgb(149, 136, 120);
        footer.BackColor = Color.FromArgb(253, 251, 248);
        footer.Font = new Font("Microsoft YaHei UI", 8f);
        footer.ContextMenuStrip = menu;
        view.DefaultBackgroundColor = Color.White;
        Controls.AddRange(new Control[] { view, header, footer });

        BuildMenu();
        tray.Icon = Icon;
        tray.Text = "仓鼠小窝 · 双击打开";
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { RestoreWidget(); };
        tray.Visible = Program.QaDirectory == null;
        ApplySize(preferences.StageWidth, false);
        var area = Screen.PrimaryScreen.WorkingArea;
        Location = preferences.Positioned ? new Point(preferences.Left, preferences.Top) : new Point(area.Right - Width - S(28), area.Bottom - Height - S(28));
        KeepOnScreen();
        SyncTopButton();
        Shown += async delegate { await InitializePlayer(); };
        Resize += delegate { LayoutControls(); };
        FormClosing += delegate { SavePreferences(); tray.Visible = false; };
        FormClosed += delegate { timeout.Dispose(); tray.Dispose(); tips.Dispose(); menu.Dispose(); };
        Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Color.FromArgb(220, 209, 194))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); };
        timeout.Interval = 45000;
        timeout.Tick += delegate { timeout.Stop(); ReportFailure("加载超时，请尝试菜单里的“重新加载”。"); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int rounded = 2; DwmSetWindowAttribute(Handle, 33, ref rounded, 4); } catch { }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x8001) { RestoreWidget(); return; }
        base.WndProc(ref message);
    }
    private int S(int value) { return (int)Math.Round(value * scale); }
    private void CreateButton(Button button, string text, string tip, EventHandler action)
    {
        button.Text = text;
        button.AccessibleName = tip;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(237, 228, 214);
        button.ForeColor = Color.FromArgb(122, 103, 78);
        button.Cursor = Cursors.Hand;
        button.TabStop = false;
        button.Margin = Padding.Empty;
        button.Click += action;
        tips.SetToolTip(button, tip);
    }
    private void LayoutControls()
    {
        int width = ClientSize.Width - 2;
        int head = S(34), foot = S(23), button = S(32), pinWidth = S(48);
        header.SetBounds(1, 1, width, head);
        footer.SetBounds(1, ClientSize.Height - foot - 1, width, foot);
        view.SetBounds(1, head + 1, width, ClientSize.Height - head - foot - 2);
        title.SetBounds(0, 0, Math.Max(0, width - pinWidth - button * 3), head);
        pin.SetBounds(width - pinWidth - button * 3, 0, pinWidth, head);
        more.SetBounds(width - button * 3, 0, button, head);
        hide.SetBounds(width - button * 2, 0, button, head);
        exit.SetBounds(width - button, 0, button, head);
    }
    private void BuildMenu()
    {
        menu.Font = Font;
        menu.Items.Add("显示小窝", null, delegate { RestoreWidget(); });
        topItem = new ToolStripMenuItem("始终置顶", null, delegate { ToggleTop(); });
        menu.Items.Add(topItem);
        var sizes = new ToolStripMenuItem("小窝大小");
        sizes.DropDownItems.Add("小 · 280", null, delegate { ApplySize(280, true); });
        sizes.DropDownItems.Add("中 · 360", null, delegate { ApplySize(360, true); });
        sizes.DropDownItems.Add("大 · 480", null, delegate { ApplySize(480, true); });
        menu.Items.Add(sizes);
        pauseItem = new ToolStripMenuItem("暂停动画", null, async delegate { paused = !paused; pauseItem.Text = paused ? "继续动画" : "暂停动画"; await UpdatePlayback(); });
        menu.Items.Add(pauseItem);
        menu.Items.Add("收起到托盘", null, delegate { HideWidget(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("重新加载", null, delegate { if (view.CoreWebView2 != null) { ready = false; failed = false; timeout.Start(); view.Reload(); } });
        menu.Items.Add("使用说明与原作者", null, delegate { MessageBox.Show(this, "单击空白处投喂；点击滚轮中央让仓鼠运动。\n拖动顶部标题移动小窝，点“置顶”切换置顶。\n“−”收起到托盘，双击托盘图标恢复；“×”退出。\n再次双击桌面图标也能唤回小窝。\n\n动画：aBowman（原版文件未修改）\nhttps://www.abowman.com/gadgets/hamster/\n播放引擎：Ruffle（MIT / Apache 2.0）\n本地运行，无需联网。", "仓鼠小窝", MessageBoxButtons.OK, MessageBoxIcon.Information); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, delegate { Close(); });
    }
    private void DragWindow(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        KeepOnScreen();
        SavePreferences();
    }
    private void ToggleTop() { TopMost = !TopMost; preferences.AlwaysOnTop = TopMost; SyncTopButton(); SavePreferences(); }
    private void SyncTopButton()
    {
        pin.Text = TopMost ? "已置顶" : "置顶";
        pin.ForeColor = TopMost ? Color.FromArgb(166, 106, 41) : Color.FromArgb(122, 103, 78);
        topItem.Checked = TopMost;
    }
    private void ApplySize(int stageWidth, bool save)
    {
        if (stageWidth != 280 && stageWidth != 360 && stageWidth != 480) stageWidth = 360;
        preferences.StageWidth = stageWidth;
        ClientSize = new Size(S(stageWidth) + 2, S(stageWidth * 3 / 4) + S(34) + S(23) + 2);
        LayoutControls();
        if (save) { KeepOnScreen(); SavePreferences(); }
    }
    private void KeepOnScreen()
    {
        var area = Screen.FromRectangle(Bounds).WorkingArea;
        Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
    }
    private async void HideWidget() { Hide(); await UpdatePlayback(); }
    private async void RestoreWidget() { Show(); WindowState = FormWindowState.Normal; KeepOnScreen(); Activate(); await UpdatePlayback(); }
    private async Task UpdatePlayback()
    {
        if (!ready || view.CoreWebView2 == null || IsDisposed) return;
        try { await view.ExecuteScriptAsync("window.setHamsterPaused(" + ((paused || !Visible) ? "true" : "false") + ");"); } catch (Exception e) { Program.Log(e.Message); }
    }
    private Preferences LoadPreferences()
    {
        if (Program.QaDirectory != null) return new Preferences();
        try { return json.Deserialize<Preferences>(File.ReadAllText(Path.Combine(Program.DataDirectory, "settings.json"))) ?? new Preferences(); } catch { return new Preferences(); }
    }
    private void SavePreferences()
    {
        if (Program.QaDirectory != null) return;
        preferences.Left = Left; preferences.Top = Top; preferences.Positioned = true; preferences.AlwaysOnTop = TopMost;
        try { Directory.CreateDirectory(Program.DataDirectory); File.WriteAllText(Path.Combine(Program.DataDirectory, "settings.json"), json.Serialize(preferences)); } catch (Exception e) { Program.Log(e.Message); }
    }
    private async Task InitializePlayer()
    {
        try
        {
            string profile = Program.QaDirectory == null ? Path.Combine(Program.DataDirectory, "browser") : Path.Combine(Program.QaDirectory, "browser");
            var options = new CoreWebView2EnvironmentOptions();
            options.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required";
            var environment = await CoreWebView2Environment.CreateAsync(null, profile, options);
            await view.EnsureCoreWebView2Async(environment);
            var core = view.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("hamster.local", Path.Combine(Program.Payload, "assets"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.NavigationStarting += delegate(object sender, CoreWebView2NavigationStartingEventArgs e) { if (!e.Uri.StartsWith(LocalOrigin, StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
            core.NewWindowRequested += delegate(object sender, CoreWebView2NewWindowRequestedEventArgs e) { e.Handled = true; };
            core.PermissionRequested += delegate(object sender, CoreWebView2PermissionRequestedEventArgs e) { e.State = CoreWebView2PermissionState.Deny; };
            core.DownloadStarting += delegate(object sender, CoreWebView2DownloadStartingEventArgs e) { e.Cancel = true; };
            core.WebMessageReceived += async delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
            {
                if (!e.Source.StartsWith(LocalOrigin, StringComparison.OrdinalIgnoreCase)) return;
                string message = e.TryGetWebMessageAsString();
                if (message == "ready")
                {
                    ready = true; timeout.Stop(); await UpdatePlayback();
                    if (Program.QaDirectory != null && !testing) { testing = true; await RunSelfTest(); }
                }
                else if (message == "menu") menu.Show(Cursor.Position);
                else if (message.StartsWith("error:")) ReportFailure(message.Substring(6));
            };
            core.ProcessFailed += delegate(object sender, CoreWebView2ProcessFailedEventArgs e) { ReportFailure("播放进程意外退出，可从菜单重新加载。" + e.ProcessFailedKind); };
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += delegate(object sender, CoreWebView2WebResourceRequestedEventArgs e)
            {
                if (!e.Request.Uri.StartsWith(LocalOrigin, StringComparison.OrdinalIgnoreCase) && !e.Request.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    e.Response = environment.CreateWebResourceResponse(new MemoryStream(new byte[0]), 403, "Offline widget", "Content-Type: text/plain");
            };
            if (Program.QaDirectory != null)
            {
                Directory.CreateDirectory(Program.QaDirectory);
                await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
                await core.CallDevToolsProtocolMethodAsync("Network.emulateNetworkConditions", "{\"offline\":true,\"latency\":0,\"downloadThroughput\":0,\"uploadThroughput\":0}");
            }
            timeout.Start();
            core.Navigate(LocalOrigin + "index.html");
        }
        catch (WebView2RuntimeNotFoundException) { ReportFailure("电脑缺少 Microsoft Edge WebView2。请先安装微软官方 WebView2 运行时。\nhttps://developer.microsoft.com/microsoft-edge/webview2/"); }
        catch (Exception error) { ReportFailure(error.ToString()); }
    }

    private void ReportFailure(string message)
    {
        Program.Log(message); failed = true; timeout.Stop();
        footer.Text = "加载遇到问题 · 从 ··· 菜单重新加载";
        if (Program.QaDirectory != null)
        {
            Directory.CreateDirectory(Program.QaDirectory);
            File.WriteAllText(Path.Combine(Program.QaDirectory, "result.json"), json.Serialize(new { passed = false, error = message }));
            Environment.ExitCode = 1; Close();
        }
        else MessageBox.Show(this, message, "仓鼠小窝", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RunSelfTest()
    {
        try
        {
            await Task.Delay(1800);
            string initial = await view.ExecuteScriptAsync("window.getHamsterStatus()");
            var status = json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(initial);
            if (!(bool)status["ready"] || !(bool)status["playing"] || status["metadata"] == null) throw new Exception("原版动画尚未成功运行：" + initial);
            await view.ExecuteScriptAsync("window.setHamsterPaused(true)");
            await Task.Delay(200);
            bool pausedOk = (await view.ExecuteScriptAsync("window.getHamsterStatus().playing")) == "false";
            await view.ExecuteScriptAsync("window.setHamsterPaused(false)");
            await Task.Delay(200);
            bool resumedOk = (await view.ExecuteScriptAsync("window.getHamsterStatus().playing")) == "true";
            ApplySize(280, false); bool smallOk = view.Width == S(280);
            ApplySize(480, false); bool largeOk = view.Width == S(480);
            ApplySize(360, false);
            TopMost = false; SyncTopButton(); bool unpinnedOk = !topItem.Checked;
            TopMost = true; SyncTopButton(); bool pinnedOk = topItem.Checked;
            Hide(); await UpdatePlayback();
            await Task.Delay(200);
            bool hiddenPaused = (await view.ExecuteScriptAsync("window.getHamsterStatus().playing")) == "false";
            Show(); await UpdatePlayback();
            await Task.Delay(200);
            bool restoredPlaying = (await view.ExecuteScriptAsync("window.getHamsterStatus().playing")) == "true";
            await Task.Delay(600);
            using (var image = File.Create(Path.Combine(Program.QaDirectory, "offline-preview.png"))) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
            bool passed = !failed && pausedOk && resumedOk && smallOk && largeOk && unpinnedOk && pinnedOk && hiddenPaused && restoredPlaying;
            File.WriteAllText(Path.Combine(Program.QaDirectory, "result.json"), json.Serialize(new { passed, offline = true, pausedOk, resumedOk, smallOk, largeOk, unpinnedOk, pinnedOk, hiddenPaused, restoredPlaying, initial = status }));
            Environment.ExitCode = passed ? 0 : 1;
            Close();
        }
        catch (Exception error) { ReportFailure(error.ToString()); }
    }
}
