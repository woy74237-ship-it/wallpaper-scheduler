// タスクトレイ常駐部とエントリポイント
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WallpaperScheduler
{
    class TrayApp : ApplicationContext
    {
        readonly Settings settings;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer timer;
        readonly ToolStripMenuItem autoItem;
        readonly Control invoker;
        readonly Icon appIcon, trayIcon;
        MainForm form;
        string lastAppliedKey;

        public TrayApp()
        {
            settings = Settings.Load();
            appIcon = Theme.LoadAppIcon(Size.Empty);
            trayIcon = Theme.LoadAppIcon(SystemInformation.SmallIconSize);

            invoker = new Control();
            var forceHandle = invoker.Handle; // 他スレッドのイベントを UI スレッドへ戻すため

            menu = new ContextMenuStrip();
            Theme.StyleMenu(menu);
            var openItem = Theme.MenuItem("設定を開く", delegate { ShowSettings(); });
            openItem.Font = Theme.BodySemi;
            menu.Items.Add(openItem);
            menu.Items.Add(Theme.MenuItem("今すぐ適用", delegate { Check(true); }));
            autoItem = Theme.MenuItem("自動切り替え", delegate { TogglePause(); });
            autoItem.Checked = !settings.Paused;
            menu.Items.Add(autoItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Theme.MenuItem("このアプリについて", delegate { ShowAbout(); }));
            if (AppInfo.HasSupport)
                menu.Items.Add(Theme.MenuItem("開発を支援する", delegate { AppInfo.Open(AppInfo.SupportUrl); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Theme.MenuItem("終了", delegate { ExitThread(); }));

            tray = new NotifyIcon { Icon = trayIcon, ContextMenuStrip = menu, Text = "壁紙スケジューラ", Visible = true };
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowSettings(); };

            timer = new System.Windows.Forms.Timer { Interval = 15000 };
            timer.Tick += delegate { Check(false); };
            timer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            Check(false);
            if (settings.Entries.Count == 0) ShowSettings();
        }

        // ---- システムイベント ----
        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) RunOnUi(delegate { Check(false); });
        }

        void OnTimeChanged(object sender, EventArgs e) { RunOnUi(delegate { Check(false); }); }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock) RunOnUi(delegate { Check(false); });
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            RunOnUi(ReloadThemeIfChanged);
        }

        void RunOnUi(MethodInvoker action)
        {
            if (invoker.IsHandleCreated && !invoker.IsDisposed) invoker.BeginInvoke(action);
        }

        /// ダーク／ライトやアクセントカラーが変わったら画面を作り直す
        void ReloadThemeIfChanged()
        {
            bool dark = Theme.Dark;
            Color accent = Theme.Accent;
            Theme.Init();
            if (dark == Theme.Dark && accent == Theme.Accent) return;
            menu.BackColor = Theme.MenuBg;
            menu.ForeColor = Theme.Fg;
            if (form != null && !form.IsDisposed)
            {
                var bounds = form.Bounds;
                form.Close();
                ShowSettings();
                form.Bounds = bounds;
            }
        }

        // ---- 切り替え ----
        void TogglePause()
        {
            settings.Paused = !settings.Paused;
            TrySave();
            autoItem.Checked = !settings.Paused;
            if (form != null && !form.IsDisposed) form.RefreshStatus();
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
            if (settings.Paused) text += "\n自動切り替え停止中";
            else
            {
                var next = Scheduler.GetNext(settings.Entries, DateTime.Now);
                text += next == null ? "\nスケジュール未設定" : "\n次の切り替え: " + next.Time;
            }
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        // ---- 設定画面 ----
        /// 他スレッドから呼ばれても安全に設定画面を開く
        public void RequestShow() { RunOnUi(ShowSettings); }

        void ShowSettings()
        {
            if (form != null && !form.IsDisposed)
            {
                if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
                form.Activate();
                return;
            }
            form = new MainForm(settings, appIcon);
            form.SettingsChanged += delegate
            {
                TrySave();
                autoItem.Checked = !settings.Paused;
                Check(false);
            };
            form.ApplyNowRequested += delegate { Check(true); };
            var thisForm = form;
            form.FormClosed += delegate { if (form == thisForm) form = null; thisForm.Dispose(); };
            form.Show();
            form.Activate();
        }

        bool aboutOpen;

        void ShowAbout()
        {
            if (aboutOpen) return;
            aboutOpen = true;
            try { AboutDialog.ShowFor(form != null && !form.IsDisposed && form.Visible ? form : null); }
            finally { aboutOpen = false; }
        }

        void TrySave()
        {
            try { settings.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show("設定を保存できませんでした。\n" + ex.Message, "壁紙スケジューラ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void ExitThreadCore()
        {
            timer.Stop();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            if (form != null && !form.IsDisposed) form.Close();
            tray.Visible = false;
            tray.Dispose();
            invoker.Dispose();
            base.ExitThreadCore();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\WallpaperScheduler.SingleInstance", out createdNew))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\WallpaperScheduler.Show"))
            {
                if (!createdNew)
                {
                    // 既に起動している場合は、そちらの設定画面を前面に出して終了
                    Native.AllowSetForegroundWindow(-1);
                    showEvent.Set();
                    return;
                }
                Application.EnableVisualStyles();   // 高 DPI 対応は assets\app.manifest で宣言
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Init();
                var app = new TrayApp();
                var wait = ThreadPool.RegisterWaitForSingleObject(showEvent, delegate { app.RequestShow(); }, null, -1, false);
                Application.Run(app);
                wait.Unregister(null);
            }
        }
    }
}
