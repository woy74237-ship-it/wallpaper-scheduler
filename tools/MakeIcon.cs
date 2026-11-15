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
