// 配色・フォント・描画ヘルパー・サムネイルキャッシュ
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WallpaperScheduler
{
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
    }

    static class Glyphs
    {
        public const string Add = "", Edit = "", Delete = "", ChevronDown = "", ChevronUp = "",
            Photo = "", Warning = "", Check = "", Refresh = "";
    }

    static class Theme
    {
        public static bool Dark;
        public static bool IsWin11;
        public static float Scale = 1f;

        public static Color Accent, AccentHover, AccentPressed, OnAccent, Danger, DangerHover, OnDanger;
        public static Color Bg, Surface, MenuBg, Border, Fg, FgDim, Track, HoverOverlay, PressOverlay;

        public static Font Body, BodySemi, Small, Heading, Title, TimeFont, TimeBig, IconFont, IconSmall, IconLarge;

        public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                              TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public static void Init()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            Dark = ReadDarkMode();
            IsWin11 = ReadBuild() >= 22000;
            Color sysAccent = ReadAccent();

            if (Dark)
            {
                Bg = Rgb(0x20, 0x20, 0x20); Surface = Rgb(0x2B, 0x2B, 0x2B); MenuBg = Rgb(0x2C, 0x2C, 0x2C);
                Border = Rgb(0x3D, 0x3D, 0x3D); Fg = Color.White; FgDim = Rgb(0xA8, 0xA8, 0xA8); Track = Rgb(0x45, 0x45, 0x45);
                HoverOverlay = Color.FromArgb(20, 255, 255, 255); PressOverlay = Color.FromArgb(10, 255, 255, 255);
                Accent = Luma(sysAccent) < 0.55 ? Blend(sysAccent, Color.White, 0.45f) : sysAccent;
                Danger = Rgb(0xFF, 0x99, 0xA4);
            }
            else
            {
                Bg = Rgb(0xF3, 0xF3, 0xF3); Surface = Color.White; MenuBg = Rgb(0xF9, 0xF9, 0xF9);
                Border = Rgb(0xE0, 0xE0, 0xE0); Fg = Rgb(0x1B, 0x1B, 0x1B); FgDim = Rgb(0x6B, 0x6B, 0x6B); Track = Rgb(0xD6, 0xD6, 0xD6);
                HoverOverlay = Color.FromArgb(10, 0, 0, 0); PressOverlay = Color.FromArgb(18, 0, 0, 0);
                Accent = Luma(sysAccent) > 0.5 ? Blend(sysAccent, Color.Black, 0.35f) : sysAccent;
                Danger = Rgb(0xC4, 0x2B, 0x1C);
            }
            AccentHover = Blend(Accent, Bg, 0.12f);
            AccentPressed = Blend(Accent, Bg, 0.24f);
            OnAccent = Luma(Accent) > 0.55 ? Color.Black : Color.White;
            DangerHover = Blend(Danger, Bg, 0.12f);
            OnDanger = Luma(Danger) > 0.55 ? Color.Black : Color.White;

            string ui = FontExists("Yu Gothic UI") ? "Yu Gothic UI" : SystemFonts.MessageBoxFont.Name;
            string semi = FontExists("Yu Gothic UI Semibold") ? "Yu Gothic UI Semibold" : ui;
            string icons = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            Body = new Font(ui, 10f);
            BodySemi = new Font(semi, 10f);
            Small = new Font(ui, 9f);
            Heading = new Font(semi, 13f);
            Title = new Font(semi, 16f);
            TimeFont = new Font(semi, 17f);
            TimeBig = new Font(semi, 28f);
            IconFont = new Font(icons, 10f);
            IconSmall = new Font(icons, 8f);
            IconLarge = new Font(icons, 24f);
        }

        // ---- 値の読み取り ----
        static bool ReadDarkMode()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        static Color ReadAccent()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    object v = k == null ? null : k.GetValue("AccentColor");
                    if (v is int)
                    {
                        int abgr = (int)v;
                        return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                    }
                }
            }
            catch { }
            return Rgb(0x00, 0x67, 0xC0);
        }

        static int ReadBuild()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int b;
                    return k != null && int.TryParse(k.GetValue("CurrentBuildNumber") as string, out b) ? b : 0;
                }
            }
            catch { return 0; }
        }

        static bool FontExists(string name)
        {
            using (var fonts = new InstalledFontCollection())
                foreach (var f in fonts.Families)
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ---- 色 ----
        public static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static double Luma(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0; }

        // ---- 寸法 ----
        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static float Sf(float v) { return v * Scale; }

        // ---- 描画 ----
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Color c, RectangleF r, float radius, float width)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(c, width))
                g.DrawPath(pen, p);
        }

        /// 角丸でトリミング（cover）しながら画像を描く
        public static void DrawImageCover(Graphics g, Image img, RectangleF dest, float radius)
        {
            float s = Math.Max(dest.Width / img.Width, dest.Height / img.Height);
            float tx = dest.X + (dest.Width - img.Width * s) / 2f;
            float ty = dest.Y + (dest.Height - img.Height * s) / 2f;
            using (var brush = new TextureBrush(img, WrapMode.Clamp))
            using (var path = Round(dest, radius))
            {
                brush.Transform = new Matrix(s, 0, 0, s, tx, ty);
                var old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.FillPath(brush, path);
                g.InterpolationMode = old;
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, r, color, flags);
        }

        public static Size Measure(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return Size.Empty;
            return TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        // ---- ウィンドウ ----
        public static void ApplyWindow(Form f)
        {
            int dark = Dark ? 1 : 0;
            Native.DwmSetWindowAttribute(f.Handle, 20, ref dark, 4);          // DWMWA_USE_IMMERSIVE_DARK_MODE
            int caption = ColorTranslator.ToWin32(Bg);
            Native.DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);       // DWMWA_CAPTION_COLOR (Win11)
            int text = ColorTranslator.ToWin32(Fg);
            Native.DwmSetWindowAttribute(f.Handle, 36, ref text, 4);          // DWMWA_TEXT_COLOR (Win11)
        }

        public static void DarkScrollbars(Control c)
        {
            if (Dark) Native.SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        }

        public static Label MakeLabel(string text, Font font, Color color, Color back)
        {
            return new Label { Text = text, Font = font, ForeColor = color, BackColor = back, AutoSize = true, UseMnemonic = false };
        }

        // ---- メニュー ----
        public static void StyleMenu(ContextMenuStrip m)
        {
            m.Renderer = new ThemedMenuRenderer();
            m.Font = Body;
            m.BackColor = MenuBg;
            m.ForeColor = Fg;
            m.ShowImageMargin = false;
            m.ShowCheckMargin = true;
            m.Padding = new Padding(0, S(4), 0, S(4));
            m.HandleCreated += delegate
            {
                int round = 3; // DWMWCP_ROUNDSMALL
                Native.DwmSetWindowAttribute(m.Handle, 33, ref round, 4);
            };
        }

        public static ToolStripMenuItem MenuItem(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            item.Padding = new Padding(0, S(5), S(16), S(5));
            return item;
        }

        public static Icon CreateAppIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var sky = new LinearGradientBrush(new Rectangle(0, 2, 32, 28), Color.FromArgb(40, 90, 200), Color.FromArgb(150, 200, 255), 90f))
                using (var hill = new SolidBrush(Color.FromArgb(70, 170, 100)))
                using (var hill2 = new SolidBrush(Color.FromArgb(40, 130, 80)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var p = Round(new RectangleF(1, 3, 30, 26), 6))
                    {
                        g.FillPath(sky, p);
                        g.SetClip(p);
                        g.FillEllipse(Brushes.Gold, 19, 7, 8, 8);
                        g.FillEllipse(hill2, -6, 18, 26, 20);
                        g.FillEllipse(hill, 10, 20, 30, 18);
                        g.ResetClip();
                    }
                }
                IntPtr h = bmp.GetHicon();
                using (var tmp = System.Drawing.Icon.FromHandle(h))
                {
                    var icon = (Icon)tmp.Clone();
                    Native.DestroyIcon(h);
                    return icon;
                }
            }
        }
    }

    class ThemedMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Theme.MenuBg);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (Theme.IsWin11) return; // Win11 では DWM が角丸の枠を描く
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRound(e.Graphics, Theme.HoverOverlay,
                new RectangleF(Theme.S(4), 1, e.Item.Width - Theme.S(8), e.Item.Height - 2), Theme.S(4));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Fg : Theme.FgDim;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = new Rectangle(Theme.S(6), 0, Theme.S(20), e.Item.Height);
            Theme.DrawText(e.Graphics, Glyphs.Check, Theme.IconSmall, Theme.Accent, r, Theme.Center);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, Theme.S(8), y, e.Item.Width - Theme.S(8), y);
        }
    }

    // ------------------------------------------------------------------
    // サムネイルと平均色のキャッシュ
    // ------------------------------------------------------------------
    class Thumb
    {
        public Image Image;
        public Color Average;
        public DateTime Stamp;
    }

    static class ThumbCache
    {
        static readonly Dictionary<string, Thumb> cache = new Dictionary<string, Thumb>(StringComparer.OrdinalIgnoreCase);

        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".jfif" };
        public const string Filter = "画像ファイル|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.jfif|すべてのファイル|*.*";

        public static bool IsImageFile(string path)
        {
            try { return Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) >= 0; }
            catch { return false; }
        }

        public static Thumb Get(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                Thumb t;
                if (cache.TryGetValue(path, out t) && t.Stamp == stamp) return t;
                t = Load(path);
                if (t == null) return null;
                t.Stamp = stamp;
                cache[path] = t;
                return t;
            }
            catch { return null; }
        }

        static Thumb Load(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var img = Image.FromStream(fs, false, false))
            {
                // 表示先の最大サイズ（16:9 の cover）に収まるよう縮小
                float w = Theme.S(440), h = Theme.S(248);
                float r = Math.Min(1f, Math.Max(w / img.Width, h / img.Height));
                var bmp = new Bitmap(Math.Max(1, (int)(img.Width * r)), Math.Max(1, (int)(img.Height * r)));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(img, 0, 0, bmp.Width, bmp.Height);
                }
                return new Thumb { Image = bmp, Average = AverageColor(bmp) };
            }
        }

        static Color AverageColor(Image img)
        {
            using (var small = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(img, 0, 0, 16, 16);
                }
                long r = 0, gr = 0, b = 0;
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        var c = small.GetPixel(x, y);
                        r += c.R; gr += c.G; b += c.B;
                    }
                return Color.FromArgb((int)(r / 256), (int)(gr / 256), (int)(b / 256));
            }
        }
    }
}
