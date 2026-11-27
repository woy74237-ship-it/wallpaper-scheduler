// 紹介ページ・記事用のスクリーンショットを撮るツール（tools\make-screenshots.ps1 から src\*.cs と一緒にコンパイル）
// サンプルの壁紙（promo\wallpapers の朝・昼・夜）を 6:00・12:00・19:00 に登録した画面を開いて撮影する。
// 実際の壁紙や設定ファイルは変えない。
// 窓だけを PrintWindow で撮るので、窓の外のデスクトップ（壁紙）が写り込まない。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WallpaperScheduler;

static class Screenshots
{
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    static string outDir, wallpapers;

    static List<ScheduleEntry> Sample()
    {
        return new List<ScheduleEntry>
        {
            new ScheduleEntry { Time = "06:00", ImagePath = Path.Combine(wallpapers, "morning.jpg") },
            new ScheduleEntry { Time = "12:00", ImagePath = Path.Combine(wallpapers, "noon.jpg") },
            new ScheduleEntry { Time = "19:00", ImagePath = Path.Combine(wallpapers, "night.jpg") },
        };
    }

    static void Capture(Form f, string name)
    {
        RECT r; GetWindowRect(f.Handle, out r);
        using (var bmp = new Bitmap(r.Right - r.Left, r.Bottom - r.Top, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr dc = g.GetHdc();
                PrintWindow(f.Handle, dc, 2);  // PW_RENDERFULLCONTENT
                g.ReleaseHdc(dc);
            }
            bmp.Save(Path.Combine(outDir, name), ImageFormat.Png);
        }
        Console.WriteLine("撮影しました: " + name + " (" + (r.Right - r.Left) + "x" + (r.Bottom - r.Top) + ")");
    }

    static void Shoot(Form f, string name)
    {
        f.StartPosition = FormStartPosition.CenterScreen;
        f.ShowInTaskbar = true;
        var t = new Timer { Interval = 1500 };
        t.Tick += delegate { t.Stop(); Capture(f, name); f.Close(); };
        f.Shown += delegate
        {
            Cursor.Position = new Point(0, 0);  // マウスが重なってハイライトされないよう、画面の隅へ
            f.Refresh();
            t.Start();
        };
        Application.Run(f);
    }

    [STAThread]
    static void Main(string[] args)
    {
        outDir = args[0];
        wallpapers = args[1];
        // 画面に出す「今の時刻」を固定する（既定は 14:12 = 昼の壁紙が表示中。3 つ目の引数 "HH:mm" で変えられる）
        TimeSpan at = args.Length > 2 ? TimeSpan.Parse(args[2]) : new TimeSpan(14, 12, 0);
        var fixedNow = DateTime.Today.Add(at);
        Clock.Source = () => fixedNow;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Theme.Init();

        // 1. メイン画面（固定した時刻に合わせて、朝・昼・夜のどれかが「表示中」になる）
        var settings = new Settings { Entries = Sample(), Style = "Fill" };
        Shoot(new MainForm(settings, Theme.LoadAppIcon(Size.Empty)), "main.png");

        // 2. 追加・編集の画面（昼の壁紙）
        Shoot(new EntryDialog(Sample()[1], false), "edit.png");
    }
}
