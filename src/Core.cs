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
[assembly: System.Reflection.AssemblyVersion("1.4.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.4.1.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.4.1")]

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
        // 応援先（OFUSE / Ko-fi）。OFUSE はファンレターを書く形なので、気軽に応援したい人向けに Ko-fi も選べるようにする
        public const string KofiUrl = "https://ko-fi.com/waltowen404923";
        const string OfuseItem = "OFUSE で応援する（メッセージを添えて・円）";
        const string KofiItem = "Ko-fi で応援する（ワンクリック・ドル）";

        /// タスクトレイ用: 「開発を支援する」のサブメニュー（OFUSE / Ko-fi）
        public static ToolStripMenuItem SupportMenuItem()
        {
            var item = Theme.MenuItem("開発を支援する", null);
            var drop = new ContextMenuStrip();
            Theme.StyleMenu(drop);
            item.DropDown = drop;
            item.DropDownItems.Add(Theme.MenuItem(OfuseItem, delegate { Open(SupportUrl); }));
            item.DropDownItems.Add(Theme.MenuItem(KofiItem, delegate { Open(KofiUrl); }));
            return item;
        }

        /// ボタン用: ボタンの下に OFUSE / Ko-fi を選ぶメニューを出す
        public static void ShowSupportMenu(Control anchor)
        {
            var menu = new ContextMenuStrip();
            Theme.StyleMenu(menu);
            menu.Items.Add(Theme.MenuItem(OfuseItem, delegate { Open(SupportUrl); }));
            menu.Items.Add(Theme.MenuItem(KofiItem, delegate { Open(KofiUrl); }));
            menu.Closed += delegate { anchor.BeginInvoke((MethodInvoker)menu.Dispose); };
            menu.Show(anchor, new System.Drawing.Point(0, anchor.Height));
        }

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

    /// 今の時刻。アプリの中で時刻を読むときは、すべてここを通す。
    /// ふだんはパソコンの時計そのまま。スクリーンショットのツール（tools\Screenshots.cs）だけが、
    /// 決まった時刻の画面を撮るために差し替える。
    public static class Clock
    {
        public static Func<DateTime> Source = () => DateTime.Now;
        public static DateTime Now { get { return Source(); } }
    }

    [DataContract]
    public class ScheduleEntry
    {
        [DataMember(Order = 1)] public string Time { get; set; }      // "HH:mm"
        /// 表示する画像。フォルダ指定のときは、フォルダから選ばれた今の画像
        /// （古いバージョンで設定を開いても、この画像で動くようにするため）
        [DataMember(Order = 2)] public string ImagePath { get; set; }
        /// フォルダからランダムに選ぶときのフォルダ（画像 1 枚の指定なら空）
        [DataMember(Order = 3, EmitDefaultValue = false)] public string FolderPath { get; set; }
        /// ImagePath を選んだ時間帯（"yyyy-MM-dd HH:mm"）。同じ時間帯のあいだは選び直さないために使う
        [DataMember(Order = 4, EmitDefaultValue = false)] public string PickedAt { get; set; }
        /// フォルダの画像のうち、今の一巡で表示済みのファイル名。全部表示し終えるまで同じ画像は選ばない
        [DataMember(Order = 5, EmitDefaultValue = false)] public List<string> Shown { get; set; }

        public bool IsFolder { get { return !string.IsNullOrEmpty(FolderPath); } }

        public bool IsValid
        {
            get
            {
                TimeSpan ts;
                return (!string.IsNullOrEmpty(ImagePath) || IsFolder) &&
                       TimeSpan.TryParseExact(Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out ts);
            }
        }

        /// 画面に出す名前（画像はファイル名、フォルダはフォルダ名）
        public string DisplayName
        {
            get
            {
                try { return IsFolder ? Path.GetFileName(FolderPath.TrimEnd('\\', '/')) : Path.GetFileName(ImagePath); }
                catch { return IsFolder ? FolderPath : ImagePath; }
            }
        }

        /// 今の ImagePath が、指定フォルダの中に実在する画像か
        bool HasValidPick
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath) &&
                           string.Equals(Path.GetDirectoryName(Path.GetFullPath(ImagePath)).TrimEnd('\\'),
                                         Path.GetFullPath(FolderPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }
        }

        /// フォルダ指定のとき、この時間帯の画像を決める。
        /// 時間帯が新しく始まったら、まだ表示していない画像からランダムに選ぶ。同じ時間帯のあいだ（再起動しても）は変えない。
        /// 選び直したら true
        public bool EnsurePicked(DateTime now)
        {
            if (!IsFolder) return false;
            string key = Scheduler.OccurrenceKey(this, now);
            bool valid = HasValidPick;
            if (valid && PickedAt == key) return false;
            if (valid && PickedAt == null)
            {
                // 登録画面で見えていた画像を、そのまま最初の画像にする
                PickedAt = key;
                if (Shown == null) Shown = new List<string>();
                FolderImages.MarkShown(Shown, ImagePath);
                return true;
            }
            return PickNext(valid ? ImagePath : null, key);
        }

        /// 今の時間帯の画像を、別の画像に選び直す（「別の画像にする」）
        public bool Reshuffle(DateTime now)
        {
            if (!IsFolder || FolderImages.Count(FolderPath) < 2) return false;
            return PickNext(HasValidPick ? ImagePath : null, Scheduler.OccurrenceKey(this, now));
        }

        bool PickNext(string current, string key)
        {
            if (Shown == null) Shown = new List<string>();
            string next = FolderImages.PickUnseen(FolderPath, current, Shown);
            if (next == null) return false; // フォルダが無い・画像が無い
            ImagePath = next;
            PickedAt = key;
            return true;
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

        /// その項目の時間帯が始まった日時（"yyyy-MM-dd HH:mm"）。今日まだその時刻でなければ前日
        public static string OccurrenceKey(ScheduleEntry e, DateTime now)
        {
            var start = now.Date + e.TimeOfDay;
            if (e.TimeOfDay > now.TimeOfDay) start = start.AddDays(-1);
            return start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
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

    /// フォルダ内の画像の一覧（サブフォルダは含めない）
    public static class FolderImages
    {
        class Entry { public DateTime Stamp; public List<string> Files; }
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        static readonly Random random = new Random();

        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".jfif" };

        public static bool IsImageFile(string path)
        {
            try { return Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) >= 0; }
            catch { return false; }
        }

        /// 画像ファイルの一覧（名前順）。フォルダが無ければ null
        public static List<string> List(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
                // ファイルの追加・削除・名前変更でフォルダの更新日時が変わるので、それまでは一覧を使い回す
                DateTime stamp = Directory.GetLastWriteTimeUtc(folder);
                Entry e;
                if (cache.TryGetValue(folder, out e) && e.Stamp == stamp) return e.Files;
                var files = Directory.EnumerateFiles(folder).Where(IsImageFile)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                cache[folder] = new Entry { Stamp = stamp, Files = files };
                return files;
            }
            catch { return null; }
        }

        public static int Count(string folder)
        {
            var l = List(folder);
            return l == null ? -1 : l.Count;
        }

        /// ランダムに 1 枚選ぶ。exclude（前回の画像）は、ほかに画像があれば選ばない。画像が無ければ null
        public static string Pick(string folder, string exclude)
        {
            var files = List(folder);
            if (files == null || files.Count == 0) return null;
            var candidates = files.Where(f => !string.Equals(f, exclude, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 0) candidates = files;
            return candidates[random.Next(candidates.Count)];
        }

        /// 表示済みとして記録する（ファイル名で記録）
        public static void MarkShown(List<string> shown, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string name = Path.GetFileName(path);
            if (!shown.Contains(name, StringComparer.OrdinalIgnoreCase)) shown.Add(name);
        }

        /// まだ表示していない画像からランダムに 1 枚選び、表示済みに加える（シャッフル再生と同じ考え方）。
        /// current（今の画像）も表示済みとして扱う。全部表示し終えたら記録を空にして一巡し直す（今の画像は続けて選ばない）。
        /// 画像が無ければ null
        public static string PickUnseen(string folder, string current, List<string> shown)
        {
            var files = List(folder);
            if (files == null || files.Count == 0) return null;
            var names = new HashSet<string>(files.Select(f => Path.GetFileName(f)), StringComparer.OrdinalIgnoreCase);
            shown.RemoveAll(n => !names.Contains(n)); // フォルダから消えた画像は忘れる
            if (current != null && names.Contains(Path.GetFileName(current))) MarkShown(shown, current);

            var seen = new HashSet<string>(shown, StringComparer.OrdinalIgnoreCase);
            var candidates = files.Where(f => !seen.Contains(Path.GetFileName(f))).ToList();
            if (candidates.Count == 0)
            {
                // 一巡した → 最初から。ただし今の画像がすぐ続かないようにする
                shown.Clear();
                candidates = files.Where(f => !string.Equals(f, current, StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidates.Count == 0) candidates = files;
            }
            string next = candidates[random.Next(candidates.Count)];
            MarkShown(shown, next);
            return next;
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

        /// 自動起動で起動されたことを示す引数（このときは設定画面を開かずにトレイに常駐する）
        public const string AutostartArg = "--autostart";

        public static bool IsAutostartLaunch(string[] args)
        {
            return args != null && args.Any(a => string.Equals(a, AutostartArg, StringComparison.OrdinalIgnoreCase));
        }

        public static void SetEnabled(bool enabled)
        {
            SetEnabled(enabled, false);
        }

        /// clearWindowsBlock: 使う人がアプリの画面で自分からオンにしたときは true。
        /// Windows の「スタートアップ アプリ」に記録された「オフ」も消して、確実に有効にする
        public static void SetEnabled(bool enabled, bool clearWindowsBlock)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" " + AutostartArg);
                else k.DeleteValue(ValueName, false);
            }
            if (enabled && clearWindowsBlock) ClearWindowsBlock();
        }

        // Windows の設定（スタートアップ アプリ）やタスク マネージャーでのオン／オフの記録
        const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        /// 自動起動は登録されているが、Windows の設定でオフにされているか
        /// （記録の先頭バイトの最下位ビットが 1 ならオフ。例: 01, 03 = オフ、00, 02 = オン）
        public static bool IsBlockedByWindows()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(ApprovedKey))
                {
                    var b = k == null ? null : k.GetValue(ValueName) as byte[];
                    return b != null && b.Length > 0 && (b[0] & 1) == 1;
                }
            }
            catch { return false; }
        }

        /// Windows に記録された「オフ」を消す（記録が無ければオン扱いになる）
        public static void ClearWindowsBlock()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(ApprovedKey, true))
                    if (k != null) k.DeleteValue(ValueName, false);
            }
            catch { }
        }

        /// Windows の「スタートアップ アプリ」の設定画面を開く
        public static void OpenWindowsStartupSettings()
        {
            try { System.Diagnostics.Process.Start("ms-settings:startupapps"); } catch { }
        }

        static string RegisteredValue()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                var v = k == null ? null : k.GetValue(ValueName) as string;
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
        }

        /// 登録されている exe のパス（未登録なら null）。"C:\...\x.exe" --autostart の形から取り出す
        public static string RegisteredPath()
        {
            string v = RegisteredValue();
            if (v == null) return null;
            if (v.StartsWith("\""))
            {
                int end = v.IndexOf('"', 1);
                return end > 1 ? v.Substring(1, end - 1) : v.Trim('"');
            }
            return v;
        }

        /// 自動起動の登録を直す
        /// ・登録先の exe が移動・削除されて存在しない → 今動いている exe に登録し直す
        /// ・今の exe を指しているが、古い形式（--autostart なし）→ 新しい形式に更新する
        /// 別の場所にある有効な exe の登録は変えない
        public static bool RepairIfBroken()
        {
            string raw = RegisteredValue();
            string path = RegisteredPath();
            if (raw == null || path == null) return false;

            bool exists = false;
            try { exists = File.Exists(path); } catch { }
            if (!exists) { SetEnabled(true); return true; }

            bool isThisExe = false;
            try { isThisExe = string.Equals(Path.GetFullPath(path), Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase); } catch { }
            if (isThisExe && raw.IndexOf(AutostartArg, StringComparison.OrdinalIgnoreCase) < 0) { SetEnabled(true); return true; }
            return false;
        }
    }
}
