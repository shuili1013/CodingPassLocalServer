using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class TrayAppearance {
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public static bool IsLight(string setting) {
        try {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return key == null || Convert.ToInt32(key.GetValue(setting, 1)) != 0;
        } catch { return true; }
    }
    public static Icon CodeIcon(bool darkBackground, int size, bool active = true) {
        using (Bitmap bitmap = new Bitmap(size, size))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (FontFamily family = new FontFamily("Consolas"))
        using (GraphicsPath text = new GraphicsPath()) {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            text.AddString("</>", family, (int)FontStyle.Bold, 32, PointF.Empty, StringFormat.GenericTypographic);
            RectangleF bounds = text.GetBounds();
            float scaleX = (size - 0.5f) / bounds.Width;
            float scaleY = scaleX;
            using (Matrix transform = new Matrix(scaleX, 0, 0, scaleY,
                (size - bounds.Width * scaleX) / 2 - bounds.X * scaleX,
                (size - bounds.Height * scaleY) / 2 - bounds.Y * scaleY)) text.Transform(transform);
            Color color = active ? (darkBackground ? Color.FromArgb(72, 225, 126) : Color.FromArgb(0, 128, 65)) :
                (darkBackground ? Color.FromArgb(112,112,112) : Color.FromArgb(120,120,120));
            using (Brush brush = new SolidBrush(color))
                graphics.FillPath(brush, text);
            IntPtr handle = bitmap.GetHicon();
            try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }
    public static GraphicsPath Round(RectangleF rectangle, float radius) {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rectangle.Left, rectangle.Top, d, d, 180, 90);
        path.AddArc(rectangle.Right - d, rectangle.Top, d, d, 270, 90);
        path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class FluentMenu : ContextMenuStrip {
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public bool Dark;
    public float DpiScale;
    public FluentMenu() {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) DpiScale = g.DpiX / 96f;
        Font = new Font("Segoe UI", 10f);
        Padding = new Padding((int)(6 * DpiScale));
        ShowImageMargin = false;
        ShowCheckMargin = true;
        DropShadowEnabled = true;
        ApplyTheme(null);
        Opening += delegate { ApplyTheme(null); };
    }
    public void ApplyTheme(bool? forcedDark) {
        Dark = forcedDark.HasValue ? forcedDark.Value : !TrayAppearance.IsLight("AppsUseLightTheme");
        if (SystemInformation.HighContrast) {
            Renderer = new ToolStripSystemRenderer();
            BackColor = SystemColors.Menu;
            ForeColor = SystemColors.MenuText;
        } else {
            Renderer = new FluentRenderer(Dark, DpiScale);
            BackColor = Dark ? Color.FromArgb(43,43,43) : Color.FromArgb(249,249,249);
            ForeColor = Dark ? Color.FromArgb(245,245,245) : Color.FromArgb(28,28,28);
        }
        foreach (ToolStripItem item in Items) {
            item.ForeColor = ForeColor;
            if (!(item is ToolStripSeparator)) {
                item.AutoSize = false;
                item.Size = new Size((int)(260 * DpiScale), (int)(36 * DpiScale));
            }
        }
        if (IsHandleCreated) SetCorners();
        Invalidate();
    }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        SetCorners();
    }
    void SetCorners() {
        try {
            int round = 2, dark = Dark ? 1 : 0;
            DwmSetWindowAttribute(Handle, 33, ref round, 4);
            DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
}

internal sealed class FluentRenderer : ToolStripProfessionalRenderer {
    readonly bool dark;
    readonly float scale;
    Color Background { get { return dark ? Color.FromArgb(43,43,43) : Color.FromArgb(249,249,249); } }
    public FluentRenderer(bool isDark, float dpiScale) { dark = isDark; scale = dpiScale; RoundedEdges = false; }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Background); }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = TrayAppearance.Round(new RectangleF(0.5f,0.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1), 8*scale))
        using (Pen pen = new Pen(dark ? Color.FromArgb(68,68,68) : Color.FromArgb(220,220,220))) e.Graphics.DrawPath(pen, path);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
        if (!e.Item.Enabled || !e.Item.Selected) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = TrayAppearance.Round(new RectangleF(1,1,e.Item.Width-2,e.Item.Height-2), 4*scale))
        using (Brush brush = new SolidBrush(dark ? Color.FromArgb(63,63,63) : Color.FromArgb(235,235,235))) e.Graphics.FillPath(brush,path);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
        e.TextColor = e.Item.Enabled ? (dark ? Color.FromArgb(245,245,245) : Color.FromArgb(28,28,28)) :
            (dark ? Color.FromArgb(166,166,166) : Color.FromArgb(110,110,110));
        Rectangle bounds = new Rectangle(e.TextRectangle.X, 0, e.TextRectangle.Width, e.Item.Height);
        TextFormatFlags format = (e.TextFormat & ~TextFormatFlags.Bottom & ~TextFormatFlags.WordBreak)
            | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, e.TextColor, format);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {
        using (Pen pen = new Pen(dark ? Color.FromArgb(64,64,64) : Color.FromArgb(226,226,226)))
            e.Graphics.DrawLine(pen, 8*scale, e.Item.Height/2, e.Item.Width-8*scale, e.Item.Height/2);
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = e.ImageRectangle;
        r.Y = (e.Item.Height - r.Height) / 2;
        using (Pen pen = new Pen(dark ? Color.FromArgb(153,210,255) : Color.FromArgb(0,95,184), 1.8f*scale)) {
            pen.StartCap = pen.EndCap = LineCap.Round;
            e.Graphics.DrawLines(pen, new PointF[] {
                new PointF(r.Left+2*scale,r.Top+r.Height*0.5f),
                new PointF(r.Left+r.Width*0.43f,r.Bottom-3*scale),
                new PointF(r.Right-1*scale,r.Top+3*scale) });
        }
    }
}
