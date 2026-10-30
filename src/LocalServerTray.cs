using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class BridgeState {
    public int Id { get; set; }
    public string StartTicks { get; set; }
}

internal sealed class BridgeTray : ApplicationContext {
    readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    readonly NotifyIcon icon = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly EventWaitHandle stopRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\CodingPassLocalServerStop");
    readonly ToolStripMenuItem status = new ToolStripMenuItem();
    readonly ToolStripMenuItem autoStart = new ToolStripMenuItem("登入 Windows 時自動啟動");
    readonly ToolStripMenuItem enableService = new ToolStripMenuItem("啟用服務");
    FluentMenu menu;
    bool? darkTaskbar;
    bool? greenIcon;
    bool serviceReady;
    bool startup;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "CodingPassLocalServer";
    Process judge;
    BridgeState identity;
    int ticks;
    string Exe { get { return Path.Combine(root, "CodingPass.Judge.exe"); } }
    string StateFile { get { return Path.Combine(root, "local-server.state.json"); } }

    delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);

    static bool Running(string name) {
        Process[] processes = Process.GetProcessesByName(name);
        bool found = processes.Length != 0;
        foreach (Process p in processes) p.Dispose();
        return found;
    }

    bool Owns(Process process, BridgeState saved) {
        try {
            return process != null && saved != null && !process.HasExited &&
                process.Id == saved.Id &&
                String.Equals(process.MainModule.FileName, Exe, StringComparison.OrdinalIgnoreCase) &&
                process.StartTime.ToUniversalTime().Ticks.ToString() == saved.StartTicks;
        } catch { return false; }
    }

    static bool Ready() {
        try {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:24680/");
            request.Method = "OPTIONS";
            request.Proxy = null;
            request.Timeout = 700;
            request.ReadWriteTimeout = 700;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                return response.StatusCode == HttpStatusCode.NoContent;
        } catch { return false; }
    }

    public BridgeTray(bool atStartup) {
        startup = atStartup;
        StartService();
        menu = new FluentMenu();
        status.Enabled = false;
        status.Text = "正在連線…";
        menu.Items.Add(status);
        enableService.Click += delegate { ChangeService(!Owns(judge, identity)); };
        menu.Items.Add(enableService);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("開啟 CodingPass 網站", null, delegate {
            try { Process.Start(new ProcessStartInfo("https://www.codingpass.com") { UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(error.Message, "CodingPass 本機服務"); }
        });
        menu.Items.Add("查看狀態", null, delegate { ShowStatus(); });
        menu.Items.Add(new ToolStripSeparator());
        autoStart.Checked = AutoStartEnabled();
        autoStart.Click += delegate {
            try {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey)) {
                    if (AutoStartEnabled()) key.DeleteValue(RunName, false);
                    else key.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --startup", RegistryValueKind.String);
                }
                autoStart.Checked = AutoStartEnabled();
            } catch (Exception error) { MessageBox.Show(error.Message, "CodingPass 本機服務"); }
        };
        menu.Items.Add(autoStart);
        menu.Opening += delegate { autoStart.Checked = AutoStartEnabled(); UpdateServiceMenu(); };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("停止服務並結束", null, delegate { StopAndExit(); });
        RefreshAppearance();
        icon.Text = "CodingPass 本機服務";
        icon.ContextMenuStrip = menu;
        icon.DoubleClick += delegate { ShowStatus(); };
        icon.Visible = true;
        timer.Interval = 500;
        timer.Tick += delegate { Update(); };
        timer.Start();
        Update();
        if (!startup) icon.ShowBalloonTip(3000, "CodingPass 本機服務", "已在背景執行。右鍵可啟用、停用或結束服務。", ToolTipIcon.Info);
    }

    void StartService() {
        if (Running("CodingPass.Exam")) throw new Exception("考試程式正在執行，請結束後再開啟本機服務。");
        if (Running("CodingPass.Main")) throw new Exception("請先正常關閉 CodingPass 主視窗，再開啟此背景程式。");
        if (!File.Exists(Exe)) throw new Exception("請將此程式放在 CodingPass.Judge.exe 同一個資料夾。");
        if (Owns(judge, identity)) return;
        if (judge != null) judge.Dispose();
        judge = null;
        identity = null;
        if (File.Exists(StateFile)) {
            try {
                identity = new JavaScriptSerializer().Deserialize<BridgeState>(File.ReadAllText(StateFile));
                Process candidate = Process.GetProcessById(identity.Id);
                if (Owns(candidate, identity)) judge = candidate;
                else candidate.Dispose();
            } catch { identity = null; }
        }
        if (judge == null) {
            if (Running("CodingPass.Judge") || Ready())
                throw new Exception("已有其他 CodingPass 服務，請先用原入口關閉，再啟動背景程式。");
            ProcessStartInfo start = new ProcessStartInfo(Exe, "start=go");
            start.WorkingDirectory = root;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            judge = Process.Start(start);
            identity = new BridgeState { Id = judge.Id, StartTicks = judge.StartTime.ToUniversalTime().Ticks.ToString() };
            try { File.WriteAllText(StateFile, new JavaScriptSerializer().Serialize(identity)); }
            catch {
                if (Owns(judge, identity)) judge.Kill();
                throw;
            }
        }
    }

    void UpdateServiceMenu() {
        bool exam = Running("CodingPass.Exam");
        bool active = Owns(judge, identity);
        enableService.Enabled = !exam;
        enableService.Checked = active;
    }

    void ChangeService(bool enable) {
        try {
            if (enable) StartService(); else StopService();
        } catch (Exception error) { MessageBox.Show(error.Message, "CodingPass 本機服務"); }
        ticks = 0;
        Update();
    }

    static bool AutoStartEnabled() {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey)) {
            string expected = "\"" + Application.ExecutablePath + "\" --startup";
            return key != null && String.Equals(key.GetValue(RunName) as string, expected, StringComparison.OrdinalIgnoreCase);
        }
    }

    void RefreshAppearance() {
        bool dark = !TrayAppearance.IsLight("SystemUsesLightTheme");
        if (darkTaskbar != dark || greenIcon != serviceReady) {
            Icon old = icon.Icon;
            icon.Icon = TrayAppearance.CodeIcon(dark, Math.Max(16, SystemInformation.SmallIconSize.Width), serviceReady);
            if (old != null) old.Dispose();
            darkTaskbar = dark;
            greenIcon = serviceReady;
        }
        bool menuDark = !TrayAppearance.IsLight("AppsUseLightTheme");
        if (menu.Dark != menuDark) menu.ApplyTheme(null);
    }

    void HideBlankWindows() {
        if (!Owns(judge, identity)) return;
        EnumWindows(delegate(IntPtr window, IntPtr parameter) {
            uint process;
            GetWindowThreadProcessId(window, out process);
            // Only the owned Judge's blank placeholder; leave named dialogs alone.
            if (process == (uint)judge.Id && IsWindowVisible(window) && GetWindowTextLength(window) == 0)
                ShowWindow(window, 0);
            return true;
        }, IntPtr.Zero);
    }

    void Update() {
        if (stopRequest.WaitOne(0)) { StopAndExit(); return; }
        if (Running("CodingPass.Exam")) {
            status.Text = "考試程式執行中，暫停管理";
            icon.Text = "CodingPass：暫停管理";
            serviceReady = false;
            RefreshAppearance();
            UpdateServiceMenu();
            return;
        }
        if (!Owns(judge, identity)) {
            serviceReady = false;
            status.Text = "服務已停用";
            icon.Text = "CodingPass：已停用";
            RefreshAppearance();
            UpdateServiceMenu();
            return;
        }
        HideBlankWindows();
        if (ticks++ % 10 == 0) {
            bool ready = Ready();
            serviceReady = ready;
            RefreshAppearance();
            UpdateServiceMenu();
            status.Text = ready ? "本機服務運作中" : "服務尚未回應";
            icon.Text = "CodingPass：" + status.Text;
        }
    }

    void ShowStatus() {
        MessageBox.Show(status.Text + "\nhttp://localhost:24680/\n\n勾選「啟用服務」啟動服務，取消勾選停用。\n「停止服務並結束」會一併關閉通知區程式。",
            "CodingPass 本機服務", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void StopService() {
        if (Running("CodingPass.Exam")) {
            throw new Exception("考試程式正在執行，暫不停止服務。");
        }
        if (Owns(judge, identity)) {
            judge.Kill();
            if (!judge.WaitForExit(3000)) throw new Exception("服務仍在結束中，請稍後再試。");
        }
        if (identity != null && File.Exists(StateFile)) {
            BridgeState saved = new JavaScriptSerializer().Deserialize<BridgeState>(File.ReadAllText(StateFile));
            if (saved != null && saved.Id == identity.Id && saved.StartTicks == identity.StartTicks) File.Delete(StateFile);
        }
        if (judge != null) judge.Dispose();
        judge = null;
        identity = null;
        serviceReady = false;
    }

    void StopAndExit() {
        try {
            StopService();
            ExitThread();
        } catch (Exception error) { MessageBox.Show(error.Message, "CodingPass 本機服務"); }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            timer.Stop(); timer.Dispose();
            stopRequest.Dispose();
            icon.Visible = false;
            if (icon.Icon != null) icon.Icon.Dispose();
            if (icon.ContextMenuStrip != null) icon.ContextMenuStrip.Dispose();
            icon.Dispose();
            if (judge != null) judge.Dispose();
        }
        base.Dispose(disposing);
    }

    [STAThread]
    static void Main(string[] args) {
        if (Array.IndexOf(args, "--shutdown") >= 0) {
            foreach (Process process in Process.GetProcessesByName("CodingPassLocalServer")) {
                try {
                    if (process.Id == Process.GetCurrentProcess().Id ||
                        !String.Equals(process.MainModule.FileName, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (Running("CodingPass.Exam")) { Environment.ExitCode = 1; return; }
                    using (EventWaitHandle signal = EventWaitHandle.OpenExisting("Local\\CodingPassLocalServerStop")) signal.Set();
                    if (!process.WaitForExit(10000)) Environment.ExitCode = 1;
                } catch { Environment.ExitCode = 1; }
                finally { process.Dispose(); }
            }
            return;
        }
        bool startup = Array.IndexOf(args, "--startup") >= 0;
        bool created;
        using (Mutex mutex = new Mutex(true, "Local\\CodingPassLocalServerTray", out created)) {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { using (BridgeTray app = new BridgeTray(startup)) Application.Run(app); }
            catch (Exception error) {
                if (!startup) MessageBox.Show(error.Message, "CodingPass 本機服務", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local-server-startup-error.txt"), DateTime.Now + "\r\n" + error.Message); } catch { }
            }
            finally { mutex.ReleaseMutex(); }
        }
    }
}
