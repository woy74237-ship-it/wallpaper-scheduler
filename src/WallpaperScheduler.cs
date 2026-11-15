// 壁紙スケジューラ - 指定した時刻に Windows の壁紙を切り替える常駐アプリ
// .NET Framework 4.x 標準の csc.exe でビルドできるよう C# 5 の構文のみを使用しています。
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Wallpaper Scheduler")]
[assembly: System.Reflection.AssemblyProduct("Wallpaper Scheduler")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace WallpaperScheduler
{
    // ------------------------------------------------------------------
    // データ
    // ------------------------------------------------------------------
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

        public ScheduleEntry Clone()
        {
            return new ScheduleEntry { Time = Time, ImagePath = ImagePath };
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

    // ------------------------------------------------------------------
    // スケジュール計算
    // ------------------------------------------------------------------
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
    }

    // ------------------------------------------------------------------
    // 壁紙の設定
    // ------------------------------------------------------------------
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

    // ------------------------------------------------------------------
    // Windows 起動時の自動実行（HKCU\...\Run）
    // ------------------------------------------------------------------
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

    // ------------------------------------------------------------------
    // ユーティリティ
    // ------------------------------------------------------------------
    static class Util
    {
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public const string ImageFilter = "画像ファイル|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.jfif|すべてのファイル|*.*";

        /// ファイルをロックせずに縮小画像を読み込む
        public static Image LoadThumbnail(string path, Size max)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                using (var fs = File.OpenRead(path))
                using (var img = Image.FromStream(fs))
                {
                    double r = Math.Min(1.0, Math.Min((double)max.Width / img.Width, (double)max.Height / img.Height));
                    return new Bitmap(img, Math.Max(1, (int)(img.Width * r)), Math.Max(1, (int)(img.Height * r)));
                }
            }
            catch { return null; }
        }

        public static void SetImage(PictureBox box, Image img)
        {
            var old = box.Image;
            box.Image = img;
            if (old != null) old.Dispose();
        }

        public static Icon CreateAppIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var sky = new LinearGradientBrush(new Rectangle(0, 4, 32, 24), Color.FromArgb(50, 120, 220), Color.FromArgb(170, 215, 255), 90f))
                using (var hill = new SolidBrush(Color.FromArgb(60, 160, 80)))
                using (var frame = new Pen(Color.FromArgb(30, 50, 90), 2))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    g.FillRectangle(sky, 2, 4, 28, 24);
                    g.FillEllipse(Brushes.Gold, 19, 7, 8, 8);
                    g.FillPolygon(hill, new[] {
                        new Point(2, 28), new Point(2, 22), new Point(11, 13), new Point(19, 21),
                        new Point(23, 17), new Point(30, 24), new Point(30, 28) });
                    g.DrawRectangle(frame, 2, 4, 28, 24);
                }
                IntPtr h = bmp.GetHicon();
                using (var tmp = Icon.FromHandle(h))
                {
                    var icon = (Icon)tmp.Clone();
                    DestroyIcon(h);
                    return icon;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // タスクトレイ常駐部
    // ------------------------------------------------------------------
    class TrayApp : ApplicationContext
    {
        readonly Settings settings;
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer timer;
        readonly ToolStripMenuItem pauseItem;
        readonly Control invoker;
        readonly Icon appIcon;
        SettingsForm openForm;
        string lastAppliedKey;

        public TrayApp()
        {
            settings = Settings.Load();
            appIcon = Util.CreateAppIcon();

            invoker = new Control();
            var forceHandle = invoker.Handle; // UI スレッドへ戻すためのハンドルを作成

            var menu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem("設定を開く(&S)...", null, delegate { ShowSettings(); });
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            menu.Items.Add(openItem);
            menu.Items.Add(new ToolStripMenuItem("今すぐ適用(&A)", null, delegate { Check(true); }));
            pauseItem = new ToolStripMenuItem("一時停止(&P)", null, delegate { TogglePause(); });
            pauseItem.Checked = settings.Paused;
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("終了(&X)", null, delegate { ExitThread(); }));

            tray = new NotifyIcon { Icon = appIcon, ContextMenuStrip = menu, Text = "壁紙スケジューラ", Visible = true };
            tray.MouseDoubleClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowSettings(); };

            timer = new System.Windows.Forms.Timer { Interval = 15000 };
            timer.Tick += delegate { Check(false); };
            timer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            Check(false);
            if (settings.Entries.Count == 0) ShowSettings();
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) RunOnUi(delegate { Check(false); });
        }

        void OnTimeChanged(object sender, EventArgs e) { RunOnUi(delegate { Check(false); }); }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock) RunOnUi(delegate { Check(false); });
        }

        void RunOnUi(MethodInvoker action)
        {
            if (invoker.IsHandleCreated && !invoker.IsDisposed) invoker.BeginInvoke(action);
        }

        void TogglePause()
        {
            settings.Paused = !settings.Paused;
            pauseItem.Checked = settings.Paused;
            TrySave();
            Check(false);
        }

        void Check(bool force)
        {
            if (!settings.Paused || force)
            {
                var active = Scheduler.GetActive(settings.Entries, DateTime.Now);
                if (active != null)
                {
                    string key = active.Time + "|" + active.ImagePath + "|" + settings.Style;
                    if (force || key != lastAppliedKey)
                    {
                        lastAppliedKey = key;
                        try { Wallpaper.Set(active.ImagePath, settings.Style); }
                        catch (Exception ex)
                        {
                            tray.ShowBalloonTip(5000, "壁紙を変更できませんでした",
                                active.Time + "  " + active.ImagePath + "\n" + ex.Message, ToolTipIcon.Warning);
                        }
                    }
                }
            }
            UpdateTooltip();
        }

        void UpdateTooltip()
        {
            string text = "壁紙スケジューラ";
            if (settings.Paused) text += "\n一時停止中";
            else
            {
                var next = Scheduler.GetNext(settings.Entries, DateTime.Now);
                text += next == null ? "\nスケジュール未設定" : "\n次の切り替え: " + next.Time;
            }
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        void ShowSettings()
        {
            if (openForm != null && !openForm.IsDisposed)
            {
                if (openForm.WindowState == FormWindowState.Minimized) openForm.WindowState = FormWindowState.Normal;
                openForm.Activate();
                return;
            }
            openForm = new SettingsForm(settings, appIcon);
            openForm.FormClosed += delegate
            {
                if (openForm.DialogResult == DialogResult.OK)
                {
                    settings.Entries = openForm.Entries;
                    settings.Style = openForm.SelectedStyle;
                    TrySave();
                    try { Startup.SetEnabled(openForm.StartupEnabled); }
                    catch (Exception ex) { MessageBox.Show("自動起動の設定に失敗しました。\n" + ex.Message, "壁紙スケジューラ", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    Check(true);
                }
                openForm.Dispose();
                openForm = null;
            };
            openForm.Show();
            openForm.Activate();
        }

        void TrySave()
        {
            try { settings.Save(); }
            catch (Exception ex) { MessageBox.Show("設定を保存できませんでした。\n" + ex.Message, "壁紙スケジューラ", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        protected override void ExitThreadCore()
        {
            timer.Stop();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            if (openForm != null && !openForm.IsDisposed) openForm.Close();
            tray.Visible = false;
            tray.Dispose();
            invoker.Dispose();
            base.ExitThreadCore();
        }
    }

    // ------------------------------------------------------------------
    // 設定画面
    // ------------------------------------------------------------------
    class SettingsForm : Form
    {
        readonly ListView list;
        readonly PictureBox preview;
        readonly ComboBox styleBox;
        readonly CheckBox startupBox;
        readonly Button editButton, deleteButton;
        readonly Icon icon;

        public List<ScheduleEntry> Entries { get; private set; }
        public string SelectedStyle { get { return Wallpaper.Styles[Math.Max(0, styleBox.SelectedIndex)]; } }
        public bool StartupEnabled { get { return startupBox.Checked; } }

        public SettingsForm(Settings s, Icon icon)
        {
            this.icon = icon;
            Entries = s.Entries.Select(e => e.Clone()).ToList();

            Text = "壁紙スケジューラ - 設定";
            Icon = icon;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(660, 430);
            MinimumSize = new Size(560, 380);
            StartPosition = FormStartPosition.CenterScreen;

            var hint = new Label
            {
                Text = "指定した時刻になると壁紙が切り替わります（毎日繰り返し）。",
                Location = new Point(12, 12), AutoSize = true
            };

            list = new ListView
            {
                View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, GridLines = true,
                Location = new Point(12, 36), Size = new Size(486, 290),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            list.Columns.Add("時刻", 70);
            list.Columns.Add("画像ファイル", 400);
            list.SelectedIndexChanged += delegate { UpdateSelection(); };
            list.DoubleClick += delegate { EditSelected(); };
            list.KeyDown += delegate(object o, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) DeleteSelected();
                else if (e.KeyCode == Keys.Enter) EditSelected();
            };
            list.AllowDrop = true;
            list.DragEnter += delegate(object o, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            list.DragDrop += delegate(object o, DragEventArgs e)
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) AddEntry(files[0]);
            };

            int rx = 510;
            var addButton = MakeButton("追加(&A)...", rx, 36, delegate { AddEntry(null); });
            editButton = MakeButton("編集(&E)...", rx, 72, delegate { EditSelected(); });
            deleteButton = MakeButton("削除(&D)", rx, 108, delegate { DeleteSelected(); });
            foreach (var b in new[] { addButton, editButton, deleteButton }) b.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            preview = new PictureBox
            {
                Location = new Point(rx, 152), Size = new Size(138, 100), SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 32, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var styleLabel = new Label { Text = "表示方法:", Location = new Point(12, 343), AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            styleBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(84, 339), Width = 170,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            styleBox.Items.AddRange(Wallpaper.StyleLabels);
            styleBox.SelectedIndex = Math.Max(0, Array.IndexOf(Wallpaper.Styles, s.Style));

            startupBox = new CheckBox
            {
                Text = "Windows の起動時に自動で開始する", Location = new Point(12, 372), AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            try { startupBox.Checked = Startup.IsEnabled(); } catch { }

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(96, 30), Location = new Point(448, 390), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Size = new Size(96, 30), Location = new Point(552, 390), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            CancelButton = cancel;

            Controls.AddRange(new Control[] { hint, list, addButton, editButton, deleteButton, preview, styleLabel, styleBox, startupBox, ok, cancel });
            RefreshList(null);
        }

        Button MakeButton(string text, int x, int y, EventHandler onClick)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(138, 30) };
            b.Click += onClick;
            return b;
        }

        ScheduleEntry Selected
        {
            get { return list.SelectedItems.Count > 0 ? (ScheduleEntry)list.SelectedItems[0].Tag : null; }
        }

        void RefreshList(ScheduleEntry select)
        {
            Entries = Scheduler.Sorted(Entries);
            var active = Scheduler.GetActive(Entries, DateTime.Now);
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var e in Entries)
            {
                bool exists = File.Exists(e.ImagePath);
                var item = new ListViewItem(new[] { e.Time, exists ? e.ImagePath : e.ImagePath + "  (見つかりません)" }) { Tag = e };
                if (!exists) item.ForeColor = Color.Firebrick;
                if (e == active) item.Font = new Font(list.Font, FontStyle.Bold);
                list.Items.Add(item);
                if (e == select) item.Selected = true;
            }
            list.EndUpdate();
            UpdateSelection();
        }

        void UpdateSelection()
        {
            var e = Selected;
            editButton.Enabled = deleteButton.Enabled = e != null;
            Util.SetImage(preview, e == null ? null : Util.LoadThumbnail(e.ImagePath, new Size(276, 200)));
        }

        void AddEntry(string initialPath)
        {
            var draft = new ScheduleEntry { Time = SuggestTime(), ImagePath = initialPath };
            using (var dlg = new EntryDialog(draft, true, icon))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Entries.RemoveAll(x => x.Time == dlg.Result.Time);
                Entries.Add(dlg.Result);
                RefreshList(dlg.Result);
            }
        }

        string SuggestTime()
        {
            // 既存の最後の時刻の 1 時間後（なければ 07:00）
            if (Entries.Count == 0) return "07:00";
            var t = Entries.Max(x => x.TimeOfDay).Add(TimeSpan.FromHours(1));
            if (t.TotalHours >= 24) t = t.Subtract(TimeSpan.FromHours(24));
            return string.Format("{0:00}:{1:00}", t.Hours, t.Minutes);
        }

        void EditSelected()
        {
            var e = Selected;
            if (e == null) return;
            using (var dlg = new EntryDialog(e, false, icon))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Entries.Remove(e);
                Entries.RemoveAll(x => x.Time == dlg.Result.Time);
                Entries.Add(dlg.Result);
                RefreshList(dlg.Result);
            }
        }

        void DeleteSelected()
        {
            var e = Selected;
            if (e == null) return;
            Entries.Remove(e);
            RefreshList(null);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Util.SetImage(preview, null);
            base.OnFormClosed(e);
        }
    }

    // ------------------------------------------------------------------
    // 項目の追加・編集ダイアログ
    // ------------------------------------------------------------------
    class EntryDialog : Form
    {
        readonly DateTimePicker timePicker;
        readonly TextBox pathBox;
        readonly PictureBox preview;

        public ScheduleEntry Result { get; private set; }

        public EntryDialog(ScheduleEntry source, bool isNew, Icon icon)
        {
            Text = isNew ? "切り替えの追加" : "切り替えの編集";
            Icon = icon;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 320);

            var timeLabel = new Label { Text = "切り替え時刻:", Location = new Point(12, 18), AutoSize = true };
            timePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true,
                Location = new Point(110, 14), Width = 90
            };
            TimeSpan ts;
            if (!TimeSpan.TryParseExact(source.Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out ts)) ts = new TimeSpan(7, 0, 0);
            timePicker.Value = DateTime.Today.Add(ts);

            var pathLabel = new Label { Text = "画像ファイル:", Location = new Point(12, 56), AutoSize = true };
            pathBox = new TextBox { Location = new Point(110, 52), Width = 266, Text = source.ImagePath ?? "" };
            var browse = new Button { Text = "参照(&B)...", Location = new Point(384, 50), Size = new Size(84, 28) };
            browse.Click += delegate { Browse(); };

            preview = new PictureBox
            {
                Location = new Point(110, 88), Size = new Size(266, 180), SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 32, 32)
            };
            pathBox.TextChanged += delegate { UpdatePreview(); };

            var ok = new Button { Text = "OK", Location = new Point(276, 280), Size = new Size(92, 30) };
            var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(376, 280), Size = new Size(92, 30) };
            ok.Click += delegate { Accept(); };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { timeLabel, timePicker, pathLabel, pathBox, browse, preview, ok, cancel });
            UpdatePreview();
            Shown += delegate { if (string.IsNullOrEmpty(pathBox.Text)) Browse(); };
        }

        void Browse()
        {
            using (var ofd = new OpenFileDialog { Filter = Util.ImageFilter, Title = "壁紙にする画像を選択" })
            {
                try
                {
                    if (File.Exists(pathBox.Text)) ofd.InitialDirectory = Path.GetDirectoryName(pathBox.Text);
                }
                catch { }
                if (ofd.ShowDialog(this) == DialogResult.OK) pathBox.Text = ofd.FileName;
            }
        }

        void UpdatePreview()
        {
            Util.SetImage(preview, Util.LoadThumbnail(pathBox.Text.Trim().Trim('"'), new Size(532, 360)));
        }

        void Accept()
        {
            string path = pathBox.Text.Trim().Trim('"');
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "画像ファイルが見つかりません。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Result = new ScheduleEntry
            {
                Time = timePicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
                ImagePath = Path.GetFullPath(path)
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Util.SetImage(preview, null);
            base.OnFormClosed(e);
        }
    }

    // ------------------------------------------------------------------
    // エントリポイント
    // ------------------------------------------------------------------
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\WallpaperScheduler.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("壁紙スケジューラは既に起動しています。\nタスクトレイのアイコンから設定を開いてください。",
                        "壁紙スケジューラ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
