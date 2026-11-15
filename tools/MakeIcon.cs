// アプリアイコン (assets\app.ico) を生成するツール。tools\make-icon.ps1 から実行します。
// 小さいサイズは BMP 形式、256px は PNG 形式で格納した複数サイズの .ico を出力します。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

class MakeIcon
{
    static void Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("usage: MakeIcon <out.ico> [preview.png] | MakeIcon --avatar <out.png> [size]"); Environment.Exit(1); }
        if (args[0] == "--avatar")
        {
            // SNS・支援サービスのプロフィール用。丸く切り抜かれても欠けないよう、全面に描いて時計を内側に寄せる
            int size = args.Length > 2 ? int.Parse(args[2]) : 512;
            using (var bmp = DrawScene(size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    DrawClockBadge(g, size, 0.34f, 0.65f, 0.16f);
                }
                bmp.Save(args[1], ImageFormat.Png);
            }
            return;
        }
        if (args[0] == "--cover")
        {
            // 支援ページなどのカバー画像（文字なしの風景）
            using (var bmp = DrawCover(int.Parse(args[2]), int.Parse(args[3]))) bmp.Save(args[1], ImageFormat.Png);
            return;
        }
        if (args[0] == "--ogp")
        {
            // SNS でシェアされたときのサムネイル（アイコン＋アプリ名＋キャッチコピー）
            using (var bmp = DrawOgp(1200, 630)) bmp.Save(args[1], ImageFormat.Png);
            return;
        }
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var images = new List<byte[]>();
        foreach (int s in sizes)
            using (var bmp = Draw(s))
                images.Add(s >= 256 ? Png(bmp) : Dib(bmp));

        using (var fs = File.Create(args[0]))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }

        if (args.Length > 1)
            using (var b = Draw(256)) b.Save(args[1], ImageFormat.Png);
    }

    static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// 夕暮れの空・太陽・山並みのタイルに、時計のバッジを重ねたアイコン
    static Bitmap Draw(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var scene = DrawScene(s))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float m = s * (s <= 24 ? 0.03f : 0.06f);
            var tile = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            using (var path = Round(tile, s * 0.2f))
            using (var tb = new TextureBrush(scene, WrapMode.Clamp))
                g.FillPath(tb, path);

            if (s >= 32) DrawClockBadge(g, s);
        }
        return bmp;
    }

    static Bitmap DrawScene(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var all = new RectangleF(0, 0, s, s);

            using (var sky = new LinearGradientBrush(all, Color.Black, Color.Black, 90f))
            {
                sky.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { C(0x22, 0x33, 0xA6), C(0x7A, 0x5B, 0xD8), C(0xFF, 0xA3, 0x8B) },
                    Positions = new[] { 0f, 0.55f, 1f }
                };
                g.FillRectangle(sky, -1, -1, s + 2, s + 2);
            }

            // 太陽と光
            float cx = s * 0.64f, cy = s * 0.38f, r = s * 0.13f;
            using (var glow = new SolidBrush(Color.FromArgb(70, 0xFF, 0xE6, 0xA0)))
                g.FillEllipse(glow, cx - r * 1.7f, cy - r * 1.7f, r * 3.4f, r * 3.4f);
            using (var sun = new SolidBrush(C(0xFF, 0xDB, 0x7A)))
                g.FillEllipse(sun, cx - r, cy - r, r * 2, r * 2);

            // 奥の山
            FillRidge(g, s, C(0x5B, 0x3F, 0xA6), new[] {
                new PointF(-0.05f, 0.70f), new PointF(0.28f, 0.50f), new PointF(0.50f, 0.64f),
                new PointF(0.66f, 0.56f), new PointF(1.05f, 0.78f) });
            // 手前の丘
            FillRidge(g, s, C(0x2A, 0x22, 0x66), new[] {
                new PointF(-0.05f, 0.84f), new PointF(0.32f, 0.68f), new PointF(0.70f, 0.84f), new PointF(1.05f, 0.74f) });
        }
        return bmp;
    }

    static void FillRidge(Graphics g, int s, Color color, PointF[] normalized)
    {
        var pts = new PointF[normalized.Length];
        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(normalized[i].X * s, normalized[i].Y * s);
        using (var p = new GraphicsPath())
        using (var b = new SolidBrush(color))
        {
            p.AddCurve(pts, 0.45f);
            p.AddLine(pts[pts.Length - 1], new PointF(s * 1.05f, s * 1.05f));
            p.AddLine(new PointF(s * 1.05f, s * 1.05f), new PointF(-s * 0.05f, s * 1.05f));
            p.CloseFigure();
            g.FillPath(b, p);
        }
    }

    static void DrawClockBadge(Graphics g, int s)
    {
        DrawClockBadge(g, s, 0.75f, 0.75f, 0.21f);
    }

    static void DrawClockBadge(Graphics g, int s, float x, float y, float radius)
    {
        float cx = s * x, cy = s * y, r = s * radius;
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            g.FillEllipse(shadow, cx - r - s * 0.01f, cy - r + s * 0.015f, (r + s * 0.01f) * 2, (r + s * 0.01f) * 2);
        g.FillEllipse(Brushes.White, cx - r, cy - r, r * 2, r * 2);
        var ink = C(0x22, 0x33, 0xA6);
        using (var ring = new Pen(ink, Math.Max(1f, s * 0.03f)))
            g.DrawEllipse(ring, cx - r * 0.86f, cy - r * 0.86f, r * 1.72f, r * 1.72f);
        using (var hand = new Pen(ink, Math.Max(1.2f, s * 0.04f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(hand, cx, cy, cx, cy - r * 0.55f);
            g.DrawLine(hand, cx, cy, cx + r * 0.42f, cy + r * 0.1f);
        }
        using (var dot = new SolidBrush(ink))
            g.FillEllipse(dot, cx - s * 0.025f, cy - s * 0.025f, s * 0.05f, s * 0.05f);
    }

    // ------------------------------------------------------------------
    // 宣伝用の横長画像：左から右へ「朝 → 昼 → 夕方 → 夜」と移り変わる空
    // ------------------------------------------------------------------
    // 朝焼けのオレンジから青空へ直接つなぐと中間がくすむので、ピンクをはさむ
    static readonly Color[] DayColors = { C(0xFF, 0xB0, 0x8A), C(0xF2, 0xA7, 0xC3), C(0x8F, 0xB8, 0xF2), C(0x7A, 0x5B, 0xD8), C(0x1E, 0x26, 0x6E) };
    static readonly float[] DayPositions = { 0f, 0.17f, 0.36f, 0.66f, 1f };

    static Color Blend(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    static Color SkyAt(float x)
    {
        for (int i = 1; i < DayPositions.Length; i++)
            if (x <= DayPositions[i])
                return Blend(DayColors[i - 1], DayColors[i], (x - DayPositions[i - 1]) / (DayPositions[i] - DayPositions[i - 1]));
        return DayColors[DayColors.Length - 1];
    }

    static void DrawDayCycle(Graphics g, int w, int h)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var area = new Rectangle(-2, -2, w + 4, h + 4);
        using (var sky = new LinearGradientBrush(area, Color.Black, Color.Black, 0f))
        {
            sky.InterpolationColors = new ColorBlend { Colors = DayColors, Positions = DayPositions };
            g.FillRectangle(sky, area);
        }
        // 上を少し暗く、地平線付近を少し明るくして奥行きを出す
        using (var depth = new LinearGradientBrush(area, Color.FromArgb(50, 10, 10, 40), Color.FromArgb(0, 255, 255, 255), 90f))
            g.FillRectangle(depth, area);

        float u = h;
        // 星（夜側）
        var rnd = new Random(7);
        for (int i = 0; i < 60; i++)
        {
            float x = (float)(0.6 + rnd.NextDouble() * 0.4), y = (float)(rnd.NextDouble() * 0.6);
            float r = (float)(0.004 + rnd.NextDouble() * 0.008) * Math.Min(u, 400f);
            int alpha = (int)(255 * Math.Min(1.0, (x - 0.6) / 0.25) * (0.5 + rnd.NextDouble() * 0.5));
            using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 240)))
                g.FillEllipse(b, x * w - r, y * h - r, r * 2, r * 2);
        }
        // 太陽（朝〜昼側）
        float sx = w * 0.24f, sy = h * 0.40f, sr = u * 0.16f;
        using (var gp = new GraphicsPath())
        {
            gp.AddEllipse(sx - sr * 2.2f, sy - sr * 2.2f, sr * 4.4f, sr * 4.4f);
            using (var glow = new PathGradientBrush(gp))
            {
                glow.CenterColor = Color.FromArgb(120, 0xFF, 0xD9, 0x8A);
                glow.SurroundColors = new[] { Color.FromArgb(0, 0xFF, 0xD9, 0x8A) };
                g.FillPath(glow, gp);
            }
        }
        using (var sun = new SolidBrush(C(0xFF, 0xDB, 0x7A)))
            g.FillEllipse(sun, sx - sr, sy - sr, sr * 2, sr * 2);
        // 三日月（夜側）
        float mx = w * 0.84f, my = h * 0.32f, mr = u * 0.11f;
        Crescent(g, mx, my, mr, mr * 0.45f, -mr * 0.25f, C(0xF4, 0xF1, 0xD0));

        // 山並み
        FillRidgeWide(g, w, h, Color.FromArgb(235, 0x5B, 0x3F, 0xA6), new[] {
            new PointF(-0.02f, 0.70f), new PointF(0.12f, 0.58f), new PointF(0.26f, 0.67f), new PointF(0.42f, 0.55f),
            new PointF(0.58f, 0.68f), new PointF(0.74f, 0.57f), new PointF(0.88f, 0.67f), new PointF(1.02f, 0.60f) });
        FillRidgeWide(g, w, h, C(0x2A, 0x22, 0x66), new[] {
            new PointF(-0.02f, 0.84f), new PointF(0.18f, 0.73f), new PointF(0.38f, 0.85f),
            new PointF(0.60f, 0.75f), new PointF(0.80f, 0.86f), new PointF(1.02f, 0.77f) });
    }

    /// 三日月（4 倍の解像度で、円から少しずらした円をくり抜いてから縮小し、縁をなめらかにする）
    static void Crescent(Graphics g, float cx, float cy, float r, float dx, float dy, Color color)
    {
        const int k = 4;
        int size = (int)Math.Ceiling(r * 2 * k) + 8;
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var bg = Graphics.FromImage(bmp))
            using (var moon = new GraphicsPath())
            using (var cut = new GraphicsPath())
            using (var region = new Region())
            using (var b = new SolidBrush(color))
            {
                moon.AddEllipse(4, 4, r * 2 * k, r * 2 * k);
                cut.AddEllipse(4 + dx * k, 4 + dy * k, r * 2 * k, r * 2 * k);
                region.MakeEmpty();
                region.Union(moon);
                region.Exclude(cut);
                bg.FillRegion(b, region);
            }
            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, cx - r - 1f, cy - r - 1f, size / (float)k, size / (float)k);
            g.InterpolationMode = old;
        }
    }

    static void FillRidgeWide(Graphics g, int w, int h, Color color, PointF[] normalized)
    {
        var pts = new PointF[normalized.Length];
        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(normalized[i].X * w, normalized[i].Y * h);
        using (var p = new GraphicsPath())
        using (var b = new SolidBrush(color))
        {
            p.AddCurve(pts, 0.5f);
            p.AddLine(pts[pts.Length - 1], new PointF(w * 1.05f, h * 1.05f));
            p.AddLine(new PointF(w * 1.05f, h * 1.05f), new PointF(-w * 0.05f, h * 1.05f));
            p.CloseFigure();
            g.FillPath(b, p);
        }
    }

    static Bitmap DrawCover(int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) DrawDayCycle(g, w, h);
        return bmp;
    }

    static Bitmap DrawOgp(int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            DrawDayCycle(g, w, h);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 白いカード
            var card = new RectangleF(80, 95, w - 160, h - 190);
            using (var shadowPath = Round(new RectangleF(card.X, card.Y + 12, card.Width, card.Height), 36))
            using (var shadow = new SolidBrush(Color.FromArgb(60, 10, 10, 40)))
                g.FillPath(shadow, shadowPath);
            using (var cardPath = Round(card, 36))
            using (var white = new SolidBrush(C(0xFA, 0xFA, 0xFD)))
                g.FillPath(white, cardPath);

            // アイコン
            const int iconSize = 240;
            using (var icon = Draw(iconSize))
                g.DrawImage(icon, card.X + 60, card.Y + (card.Height - iconSize) / 2f, iconSize, iconSize);

            // 文字
            float x = card.X + 60 + iconSize + 56;
            var fmt = StringFormat.GenericTypographic;
            using (var title = new Font("Yu Gothic UI Semibold", 64, GraphicsUnit.Pixel))
            using (var body = new Font("Yu Gothic UI", 30, GraphicsUnit.Pixel))
            using (var chip = new Font("Yu Gothic UI Semibold", 22, GraphicsUnit.Pixel))
            using (var small = new Font("Yu Gothic UI", 22, GraphicsUnit.Pixel))
            using (var ink = new SolidBrush(C(0x1B, 0x1F, 0x4A)))
            using (var sub = new SolidBrush(C(0x4A, 0x4F, 0x6A)))
            using (var dim = new SolidBrush(C(0x8A, 0x8F, 0xA8)))
            using (var chipBg = new SolidBrush(C(0xEC, 0xEE, 0xFB)))
            using (var chipFg = new SolidBrush(C(0x22, 0x33, 0xA6)))
            {
                float y = card.Y + (card.Height - 268) / 2f;
                g.DrawString("壁紙スケジューラ", title, ink, x, y, fmt);
                y += 90;
                g.DrawString("指定した時刻に、壁紙が自動で切り替わる。", body, sub, x, y, fmt);
                y += 70;
                float cx = x;
                foreach (var label in new[] { "Windows 10 / 11", "無料", "インストール不要" })
                {
                    var size = g.MeasureString(label, chip, PointF.Empty, fmt);
                    var pill = new RectangleF(cx, y, size.Width + 36, 44);
                    using (var pp = Round(pill, 22)) g.FillPath(chipBg, pp);
                    g.DrawString(label, chip, chipFg, cx + 18, y + (44 - size.Height) / 2f, fmt);
                    cx = pill.Right + 12;
                }
                y += 44 + 34;
                g.DrawString("by Walt-Owen", small, dim, x, y, fmt);
            }
        }
        return bmp;
    }

    static byte[] Png(Bitmap bmp)
    {
        using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
    }

    /// ICO 用の BMP（32bpp BGRA + AND マスク、下から上の行順）
    static byte[] Dib(Bitmap bmp)
    {
        int s = bmp.Width, maskRow = ((s + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(s * s * 4 + maskRow * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            var data = bmp.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var row = new byte[s * 4];
            for (int y = s - 1; y >= 0; y--)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                w.Write(row);
            }
            bmp.UnlockBits(data);
            w.Write(new byte[maskRow * s]);
            return ms.ToArray();
        }
    }
}
