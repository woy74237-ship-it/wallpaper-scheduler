// サンプル壁紙（朝・昼・夜）を生成するツール。tools\make-promo.ps1 から実行します。
// 同じ山並みを時間帯ごとの色で描き分けるので、スケジュールで切り替わる様子が伝わりやすくなります。
// 使い方: MakeWallpapers <出力フォルダー> [幅 高さ]
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

class MakeWallpapers
{
    static void Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : ".";
        int w = args.Length > 2 ? int.Parse(args[1]) : 3840;
        int h = args.Length > 2 ? int.Parse(args[2]) : 2160;
        Directory.CreateDirectory(dir);
        Save(Morning(w, h), Path.Combine(dir, "morning.jpg"));
        Save(Noon(w, h), Path.Combine(dir, "noon.jpg"));
        Save(Night(w, h), Path.Combine(dir, "night.jpg"));
    }

    // ---------------- 各シーン ----------------
    static Bitmap Morning(int w, int h)
    {
        return Scene(w, h,
            new[] { Hex("#6A9FD8"), Hex("#A7C1E6"), Hex("#FFD3B3"), Hex("#FFB48A") }, new[] { 0f, 0.35f, 0.70f, 1f },
            g =>
            {
                Glow(g, w * 0.28f, h * 0.60f, h * 0.65f, Color.FromArgb(210, 0xFF, 0xE2, 0xB0));
                Disc(g, w * 0.28f, h * 0.60f, h * 0.07f, Hex("#FFF3D1"));
                foreach (var c in new[] { new PointF(0.55f, 0.30f), new PointF(0.80f, 0.22f), new PointF(0.15f, 0.25f), new PointF(0.68f, 0.40f) })
                    Cloud(g, w * c.X, h * c.Y, w * 0.05f, Color.FromArgb(120, 0xFF, 0xE8, 0xE2), 0.35f);
            },
            new[] { Hex("#B9ABD8"), Hex("#8C80C2"), Hex("#4B4686") }, Hex("#FFD9C0"));
    }

    static Bitmap Noon(int w, int h)
    {
        return Scene(w, h,
            new[] { Hex("#2366D0"), Hex("#4F93E8"), Hex("#B9DEFF"), Hex("#E2F2FF") }, new[] { 0f, 0.45f, 0.82f, 1f },
            g =>
            {
                Glow(g, w * 0.74f, h * 0.16f, h * 0.40f, Color.FromArgb(170, 255, 255, 245));
                Disc(g, w * 0.74f, h * 0.16f, h * 0.055f, Hex("#FFFCEB"));
                foreach (var c in new[] { new PointF(0.22f, 0.30f), new PointF(0.45f, 0.42f), new PointF(0.88f, 0.38f), new PointF(0.62f, 0.27f) })
                    Cloud(g, w * c.X, h * c.Y, w * 0.06f, Color.FromArgb(235, 255, 255, 255), 0.45f);
            },
            new[] { Hex("#8FB4DF"), Hex("#5E9E79"), Hex("#3A7856") }, Hex("#D6ECFF"));
    }

    static Bitmap Night(int w, int h)
    {
        var skyColors = new[] { Hex("#060A24"), Hex("#111948"), Hex("#2A2969"), Hex("#3B3075") };
        var skyPos = new[] { 0f, 0.50f, 0.85f, 1f };
        return Scene(w, h, skyColors, skyPos,
            g =>
            {
                float mx = w * 0.72f, my = h * 0.24f, mr = h * 0.06f;
                Stars(g, w, h, 520, new PointF(mx, my), mr * 1.6f);
                Glow(g, mx, my, h * 0.32f, Color.FromArgb(90, 0xC9, 0xD3, 0xFF));
                Crescent(g, mx, my, mr, mr * 0.45f, -mr * 0.22f, Hex("#F3F0D6"));
            },
            new[] { Hex("#2A2864"), Hex("#1B1949"), Hex("#0F0D2E") }, Hex("#3B3075"));
    }

    // ---------------- 共通の構図 ----------------
    static Bitmap Scene(int w, int h, Color[] sky, float[] skyPos, Action<Graphics> drawSky, Color[] ridges, Color haze)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var area = new Rectangle(-2, -2, w + 4, h + 4);
            using (var b = new LinearGradientBrush(area, Color.Black, Color.Black, 90f))
            {
                b.InterpolationColors = new ColorBlend { Colors = sky, Positions = skyPos };
                g.FillRectangle(b, area);
            }
            drawSky(g);

            // 奥から手前へ 3 層の山並み。シード固定なので朝・昼・夜で同じ形になる
            float[] baseY = { 0.66f, 0.75f, 0.86f };
            float[] amp = { 0.05f, 0.045f, 0.04f };
            for (int i = 0; i < 3; i++)
            {
                Ridge(g, w, h, ridges[i], RidgePoints(11 + i * 7, baseY[i], amp[i], 9 + i * 2));
                if (i < 2) Haze(g, w, h, baseY[i] - amp[i], baseY[i] + 0.08f, haze, i == 0 ? 90 : 60);
            }
        }
        Dither(bmp);
        return bmp;
    }

    static PointF[] RidgePoints(int seed, float baseY, float amp, int count)
    {
        var r = new Random(seed);
        var pts = new PointF[count];
        for (int i = 0; i < count; i++)
            pts[i] = new PointF(-0.05f + i * 1.1f / (count - 1), baseY + (float)(r.NextDouble() * 2 - 1) * amp);
        return pts;
    }

    static void Ridge(Graphics g, int w, int h, Color color, PointF[] normalized)
    {
        var pts = normalized.Select(p => new PointF(p.X * w, p.Y * h)).ToArray();
        using (var p = new GraphicsPath())
        using (var b = new SolidBrush(color))
        {
            p.AddCurve(pts, 0.5f);
            p.AddLine(pts[pts.Length - 1], new PointF(w * 1.1f, h * 1.1f));
            p.AddLine(new PointF(w * 1.1f, h * 1.1f), new PointF(-w * 0.1f, h * 1.1f));
            p.CloseFigure();
            g.FillPath(b, p);
        }
    }

    /// 山と山の間にかかる霞（下に向かって薄くなる帯）
    static void Haze(Graphics g, int w, int h, float top, float bottom, Color color, int alpha)
    {
        var r = new RectangleF(-2, h * top, w + 4, h * (bottom - top));
        using (var b = new LinearGradientBrush(r, Color.FromArgb(0, color), Color.FromArgb(alpha, color), 90f))
        {
            b.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.FromArgb(0, color), Color.FromArgb(alpha, color), Color.FromArgb(0, color) },
                Positions = new[] { 0f, 0.6f, 1f }
            };
            g.FillRectangle(b, r);
        }
    }

    static void Glow(Graphics g, float cx, float cy, float radius, Color center)
    {
        using (var p = new GraphicsPath())
        {
            p.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
            using (var b = new PathGradientBrush(p))
            {
                b.CenterColor = center;
                b.SurroundColors = new[] { Color.FromArgb(0, center) };
                b.Blend = new Blend { Factors = new[] { 0f, 0.35f, 1f }, Positions = new[] { 0f, 0.6f, 1f } };
                g.FillPath(b, p);
            }
        }
    }

    static void Disc(Graphics g, float cx, float cy, float r, Color c)
    {
        using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
    }

    /// 三日月。円から少しずらした円をくり抜き、4 倍の解像度で描いてから縮小して縁をなめらかにする
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

    /// ふんわりした楕円を重ねた雲
    static void Cloud(Graphics g, float cx, float cy, float size, Color color, float flat)
    {
        var rnd = new Random((int)(cx * 7 + cy * 13));
        for (int i = 0; i < 9; i++)
        {
            float ox = (float)(rnd.NextDouble() * 2 - 1) * size * 1.5f;
            float oy = (float)(rnd.NextDouble() * 2 - 1) * size * flat * 0.5f;
            float r = size * (0.5f + (float)rnd.NextDouble() * 0.5f);
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(cx + ox - r * 1.3f, cy + oy - r * flat * 1.4f, r * 2.6f, r * flat * 2.8f);
                using (var b = new PathGradientBrush(p))
                {
                    b.CenterColor = color;
                    b.SurroundColors = new[] { Color.FromArgb(0, color) };
                    b.FocusScales = new PointF(0.45f, 0.45f);
                    g.FillPath(b, p);
                }
            }
        }
    }

    /// 星を散らす（avoid を中心とした半径 avoidRadius の内側には置かない）
    static void Stars(Graphics g, int w, int h, int count, PointF avoid, float avoidRadius)
    {
        var rnd = new Random(3);
        for (int i = 0; i < count; i++)
        {
            float x = (float)rnd.NextDouble() * w, y = (float)Math.Pow(rnd.NextDouble(), 1.3) * h * 0.72f;
            float r = (float)(0.6 + Math.Pow(rnd.NextDouble(), 3) * 2.6) * h / 1080f;
            int alpha = (int)(255 * (1 - y / (h * 0.8f)) * (0.35 + rnd.NextDouble() * 0.65));
            float dx = x - avoid.X, dy = y - avoid.Y;
            if (dx * dx + dy * dy < avoidRadius * avoidRadius) continue;
            using (var b = new SolidBrush(Color.FromArgb(Math.Max(0, alpha), 255, 255, 240)))
                g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
        }
    }

    // ---------------- ユーティリティ ----------------
    static Color Hex(string s)
    {
        int v = Convert.ToInt32(s.TrimStart('#'), 16);
        return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
    }

    static Color SkyAt(Color[] cols, float[] pos, float t)
    {
        for (int i = 1; i < pos.Length; i++)
            if (t <= pos[i])
            {
                float k = (t - pos[i - 1]) / (pos[i] - pos[i - 1]);
                var a = cols[i - 1]; var b = cols[i];
                return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
            }
        return cols[cols.Length - 1];
    }

    /// 大きな画面でグラデーションに縞（バンディング）が出ないよう、ごく弱いノイズを加える
    static void Dither(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var d = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes = d.Stride * d.Height;
        var buf = new byte[bytes];
        Marshal.Copy(d.Scan0, buf, 0, bytes);
        var rnd = new Random(1);
        for (int i = 0; i < bytes; i += 4)
        {
            int n = rnd.Next(-2, 3);
            for (int k = 0; k < 3; k++) buf[i + k] = (byte)Math.Max(0, Math.Min(255, buf[i + k] + n));
        }
        Marshal.Copy(buf, 0, d.Scan0, bytes);
        bmp.UnlockBits(d);
    }

    static void Save(Bitmap bmp, string path)
    {
        var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using (var p = new EncoderParameters(1))
        {
            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
            bmp.Save(path, jpeg, p);
        }
        bmp.Dispose();
        Console.WriteLine("生成しました: " + Path.GetFullPath(path));
    }
}
