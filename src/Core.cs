// データ・スケジュール計算・壁紙設定・自動起動（UI に依存しない部分）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using Microsoft.Win32;

// バージョンはここで一元管理（exe のプロパティ・配布 ZIP の名前に使われます）
[assembly: System.Reflection.AssemblyTitle("壁紙スケジューラ")]
[assembly: System.Reflection.AssemblyDescription("指定した時刻に Windows の壁紙を切り替えるアプリ")]
[assembly: System.Reflection.AssemblyProduct("壁紙スケジューラ")]
[assembly: System.Reflection.AssemblyVersion("1.3.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.1.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.3.1")]

namespace WallpaperScheduler
{
    /// アプリ情報と外部リンク
    static class AppInfo
    {
        // ▼ 支援ページ（Ko-fi / OFUSE / GitHub Sponsors など）と公式ページ（ブログ記事など）の URL。
        //   https:// で始まる URL を入れると「このアプリについて」やトレイメニューにボタンが表示されます。
        //   空欄のままならボタンは表示されません。
        public const string SupportUrl = "https://ofuse.me/c0fdccc4";
        public const string WebsiteUrl = "https://zenn.dev/walt_owen/articles/3291d8d844a05d";

        public static bool HasSupport { get { return IsWebUrl(SupportUrl); } }
        public static bool HasWebsite { get { return IsWebUrl(WebsiteUrl); } }

        public static string Version
        {
            get
            {
                var a = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(AppInfo).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                return a != null ? a.InformationalVersion : "";
            }
        }

        static bool IsWebUrl(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps;
        }

        public static void Open(string url)
        {
            if (!IsWebUrl(url)) return;
            try { System.Diagnostics.Process.Start(url); } catch { }
        }
    }

    [DataContract]
    public class ScheduleEntry
    {
        [DataMember(Order = 1)] public string Time { get; set; }      // "HH:mm"
        [DataMember(Order = 2)] public string ImagePath { get; set; }

        public bool IsValid
        {
            get
            {
                TimeSpan ts;
                return !string.IsNullOrEmpty(ImagePath) &&
                       TimeSpan.TryParseExact(Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out ts);
            }
        }

        public TimeSpan TimeOfDay
        {
            get { return TimeSpan.ParseExact(Time, @"hh\:mm", CultureInfo.InvariantCulture); }
        }

        public static string Format(TimeSpan t)
        {
            return string.Format("{0:00}:{1:00}", t.Hours, t.Minutes);
        }
    }

    [DataContract]
    public class Settings
    {
        [DataMember(Order = 1)] public List<ScheduleEntry> Entries { get; set; }
        [DataMember(Order = 2)] public string Style { get; set; }
        [DataMember(Order = 3)] public bool Paused { get; set; }

        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WallpaperScheduler");

        public static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }

        public static Settings Load()
        {
            Settings s = null;
            if (File.Exists(FilePath))
            {
                try
                {
                    using (var fs = File.OpenRead(FilePath))
                        s = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(fs);
                }
                catch
                {
                    // 壊れた設定ファイルは退避してから初期化する
                    try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }
                    s = null;
                }
            }
            if (s == null) s = new Settings();
            if (s.Entries == null) s.Entries = new List<ScheduleEntry>();
            s.Entries.RemoveAll(e => e == null || !e.IsValid);
            if (Array.IndexOf(Wallpaper.Styles, s.Style) < 0) s.Style = "Fill";
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            string tmp = FilePath + ".tmp";
            using (var fs = File.Create(tmp))
                new DataContractJsonSerializer(typeof(Settings)).WriteObject(fs, this);
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
    }

    public static class Scheduler
    {
        public static List<ScheduleEntry> Sorted(IEnumerable<ScheduleEntry> entries)
        {
            return entries.Where(e => e != null && e.IsValid).OrderBy(e => e.TimeOfDay).ToList();
        }

        /// 現在時刻に有効な項目（その時刻以前で最も遅いもの。なければ前日の最後の項目）
        public static ScheduleEntry GetActive(IEnumerable<ScheduleEntry> entries, DateTime now)
        {
            var list = Sorted(entries);
            if (list.Count == 0) return null;
            ScheduleEntry active = list[list.Count - 1];
            foreach (var e in list)
                if (e.TimeOfDay <= now.TimeOfDay) active = e;
            return active;
        }

        /// 次に切り替わる項目
        public static ScheduleEntry GetNext(IEnumerable<ScheduleEntry> entries, DateTime now)
        {
            var list = Sorted(entries);
            if (list.Count == 0) return null;
            foreach (var e in list)
                if (e.TimeOfDay > now.TimeOfDay) return e;
            return list[0];
        }

        /// 「3時間12分」のような残り時間表記
        public static string Remaining(TimeSpan target, DateTime now)
        {
            var d = target - now.TimeOfDay;
            if (d <= TimeSpan.Zero) d += TimeSpan.FromDays(1);
            int total = (int)Math.Ceiling(d.TotalMinutes);
            int h = total / 60, m = total % 60;
            return h > 0 ? h + "時間" + m + "分" : m + "分";
        }
    }

    public static class Wallpaper
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        const uint SPI_SETDESKWALLPAPER = 0x0014;
        const uint SPIF_UPDATEINIFILE = 0x01;
        const uint SPIF_SENDCHANGE = 0x02;

        public static readonly string[] Styles = { "Fill", "Fit", "Stretch", "Tile", "Center", "Span" };
        public static readonly string[] StyleLabels = { "ページ幅に合わせる", "画面に合わせる", "拡大して表示", "並べて表示", "中央に表示", "スパン" };

        public static void Set(string path, string style)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("画像ファイルが見つかりません。", path);

            string wallpaperStyle = "10", tile = "0";
            switch (style)
            {
                case "Fit": wallpaperStyle = "6"; break;
                case "Stretch": wallpaperStyle = "2"; break;
                case "Tile": wallpaperStyle = "0"; tile = "1"; break;
                case "Center": wallpaperStyle = "0"; break;
                case "Span": wallpaperStyle = "22"; break;
            }
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
            {
                if (key != null)
                {
                    key.SetValue("WallpaperStyle", wallpaperStyle);
                    key.SetValue("TileWallpaper", tile);
                }
            }
            if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, Path.GetFullPath(path), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "WallpaperScheduler";

        public static bool IsEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(ValueName) != null;
        }

        public static void SetEnabled(bool enabled)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(ValueName, false);
            }
        }
    }
}
