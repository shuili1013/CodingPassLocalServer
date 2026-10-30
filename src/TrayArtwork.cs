using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal static class TrayArtwork {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string folder = AppDomain.CurrentDomain.BaseDirectory;
        using (Icon icon = TrayAppearance.CodeIcon(false, 32))
        using (FileStream output = File.Create(Path.Combine(folder, "CodingPassLocalServer.ico"))) icon.Save(output);
        foreach (bool dark in new bool[] { false, true }) {
            using (FluentMenu menu = new FluentMenu()) {
                menu.Items.Add(new ToolStripMenuItem("本機服務運作中") { Enabled = false });
                menu.Items.Add(new ToolStripMenuItem("啟用服務") { Checked = true });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("開啟 CodingPass 網站");
                menu.Items.Add("查看狀態");
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(new ToolStripMenuItem("登入 Windows 時自動啟動") { Checked = true });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("停止服務並結束");
                menu.ApplyTheme(dark);
                menu.PerformLayout();
                using (Bitmap bitmap = new Bitmap(menu.Width, menu.Height)) {
                    menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(folder, dark ? "tray-menu-dark.png" : "tray-menu-light.png"));
                }
            }
            using (Icon icon = TrayAppearance.CodeIcon(dark, 32))
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics g = Graphics.FromImage(bitmap)) {
                g.Clear(dark ? Color.FromArgb(32,32,32) : Color.FromArgb(243,243,243));
                g.DrawIcon(icon, new Rectangle(16,16,32,32));
                bitmap.Save(Path.Combine(folder, dark ? "tray-icon-dark.png" : "tray-icon-light.png"));
            }
            using (Bitmap bitmap = new Bitmap(96, 48))
            using (Graphics g = Graphics.FromImage(bitmap)) {
                g.Clear(dark ? Color.FromArgb(32,32,32) : Color.FromArgb(243,243,243));
                using (Icon active = TrayAppearance.CodeIcon(dark, 16, true))
                    g.DrawIcon(active, new Rectangle(16,16,16,16));
                using (Icon inactive = TrayAppearance.CodeIcon(dark, 16, false))
                    g.DrawIcon(inactive, new Rectangle(64,16,16,16));
                bitmap.Save(Path.Combine(folder, dark ? "tray-states-dark.png" : "tray-states-light.png"));
            }
        }
    }
}
