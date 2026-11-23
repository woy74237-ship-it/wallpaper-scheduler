// メイン画面（現在の壁紙・タイムライン・スケジュール一覧）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WallpaperScheduler
{
    /// 上部の「現在の壁紙」カード
    class HeaderPanel : SmoothControl
    {
        public ScheduleEntry Active, Next;
        public bool Paused;
        public readonly FlatButton ApplyButton, ShuffleButton;
        /// 「別の画像にする」ボタンを出すか（今の時間帯がフォルダ指定で、画像が 2 枚以上あるとき）
        public bool CanShuffle;
        public readonly ToggleSwitch AutoToggle;
        public readonly TimelineBar Timeline;

        public HeaderPanel()
        {
            BackColor = Theme.Surface;
            ApplyButton = new FlatButton { Text = "今すぐ適用", Glyph = Glyphs.Refresh, Kind = ButtonKind.Standard };
            ShuffleButton = new FlatButton { Glyph = Glyphs.Shuffle, Kind = ButtonKind.Standard, Visible = false };
            AutoToggle = new ToggleSwitch { Text = "自動切り替え" };
            Timeline = new TimelineBar();
            Controls.AddRange(new Control[] { ApplyButton, ShuffleButton, AutoToggle, Timeline });
        }

        Rectangle ThumbRect { get { return new Rectangle(Theme.S(16), Theme.S(16), Theme.S(224), Theme.S(126)); } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            var tr = ThumbRect;
            int tx = tr.Right + Theme.S(20), h = Theme.S(34);
            ApplyButton.SetBounds(tx, tr.Bottom - h, ApplyButton.PreferredWidth(), h);
            AutoToggle.FitWidth();
            AutoToggle.SetBounds(Width - Theme.S(20) - AutoToggle.Width, tr.Bottom - h, AutoToggle.Width, h);
            // 「今すぐ適用」の右に、アイコンだけのボタンで置く（狭くて入らないときは出さない）
            ShuffleButton.SetBounds(ApplyButton.Right + Theme.S(8), tr.Bottom - h, h, h);
            ShuffleButton.Visible = CanShuffle && ShuffleButton.Right + Theme.S(12) <= AutoToggle.Left;
            Timeline.SetBounds(Theme.S(16), tr.Bottom + Theme.S(18), Width - Theme.S(32), Theme.S(46));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, ParentBack);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Theme.FillRound(g, Theme.Surface, r, Theme.S(8));
            Theme.DrawRound(g, Theme.Border, r, Theme.S(8), 1f);

            var tr = ThumbRect;
            var thumb = Active == null ? null : ThumbCache.Get(Active.ImagePath);
            if (thumb != null) Theme.DrawImageCover(g, thumb.Image, tr, Theme.S(6));
            else
            {
                Theme.FillRound(g, Theme.Track, tr, Theme.S(6));
                Theme.DrawText(g, Glyphs.Photo, Theme.IconLarge, Theme.FgDim, tr, Theme.Center);
            }

            int tx = tr.Right + Theme.S(20), tw = Width - tx - Theme.S(20);
            var now = DateTime.Now;
            Theme.DrawText(g, Paused ? "現在の壁紙  ·  自動切り替えは停止中" : "現在の壁紙", Theme.Small, Theme.FgDim,
                new Rectangle(tx, Theme.S(16), tw, Theme.S(20)), Theme.Left);

            string name = Active == null ? "スケジュールがありません"
                : string.IsNullOrEmpty(Active.ImagePath) ? Active.DisplayName : Path.GetFileNameWithoutExtension(Active.ImagePath);
            Theme.DrawText(g, name, Theme.Title, Theme.Fg, new Rectangle(tx, Theme.S(38), tw, Theme.S(38)), Theme.Left);

            string sub;
            if (Active == null) sub = "「追加」から時刻と画像を登録してください";
            else if (Active.IsFolder && FolderImages.Count(Active.FolderPath) <= 0)
                sub = (FolderImages.Count(Active.FolderPath) < 0 ? "フォルダが見つかりません" : "フォルダに画像がありません") + "  ·  壁紙は変更しません";
            else if (Next == null || Next == Active) sub = "毎日この壁紙を表示します";
            else sub = Active.Time + " から表示  ·  次は " + Next.Time + "（あと " + Scheduler.Remaining(Next.TimeOfDay, now) + "）";
            Theme.DrawText(g, sub, Theme.Body, Theme.FgDim, new Rectangle(tx, Theme.S(78), tw, Theme.S(24)), Theme.Left);
        }
    }

    class MainForm : Form
    {
        readonly Settings settings;
        readonly HeaderPanel header;
        readonly Label sectionTitle, countLabel, styleLabel;
        readonly FlatButton addButton;
        readonly FlowLayoutPanel cardPanel;
        readonly DropdownButton styleDrop;
        readonly ToggleSwitch startupToggle;
        readonly FlatButton startupWarning;
        readonly FlatButton aboutButton;
        readonly Timer clock;
        readonly ToolTip tips;
        bool updating, updatingStartup;
        // 自動起動の案内を出すかどうか（Control.Visible は画面表示前だと常に false になるため別に持つ）
        bool showStartupWarning;

        public event EventHandler SettingsChanged, ApplyNowRequested, ShuffleRequested;

        public MainForm(Settings settings, Icon icon)
        {
            this.settings = settings;
            Text = "壁紙スケジューラ";
            Icon = icon;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(780), Theme.S(700));
            MinimumSize = SizeFromClientSize(new Size(Theme.S(620), Theme.S(580)));

            tips = new ToolTip();

            header = new HeaderPanel();
            header.ApplyButton.Click += delegate { if (ApplyNowRequested != null) ApplyNowRequested(this, EventArgs.Empty); };
            header.ShuffleButton.Click += delegate { if (ShuffleRequested != null) ShuffleRequested(this, EventArgs.Empty); };
            header.AutoToggle.CheckedChanged += delegate
            {
                if (updating) return;
                settings.Paused = !header.AutoToggle.Checked;
                Changed(false);
            };

            sectionTitle = Theme.MakeLabel("スケジュール", Theme.Heading, Theme.Fg, Theme.Bg);
            countLabel = Theme.MakeLabel("", Theme.Body, Theme.FgDim, Theme.Bg);
            addButton = new FlatButton { Text = "追加", Glyph = Glyphs.Add, Kind = ButtonKind.Primary };
            addButton.Click += delegate { AddEntry(null); };

            cardPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Bg, Padding = new Padding(0)
            };
            cardPanel.HorizontalScroll.Maximum = 0;
            cardPanel.AutoScroll = false;
            cardPanel.HorizontalScroll.Visible = false;
            cardPanel.AutoScroll = true;
            cardPanel.Paint += PaintEmptyState;
            cardPanel.ClientSizeChanged += delegate { FitCards(); cardPanel.Invalidate(); };
            cardPanel.HandleCreated += delegate { Theme.DarkScrollbars(cardPanel); };

            styleLabel = Theme.MakeLabel("表示方法", Theme.Body, Theme.FgDim, Theme.Bg);
            styleDrop = new DropdownButton { Items = Wallpaper.StyleLabels };
            styleDrop.SelectedIndex = Math.Max(0, Array.IndexOf(Wallpaper.Styles, settings.Style));
            styleDrop.SelectedIndexChanged += delegate
            {
                if (updating) return;
                settings.Style = Wallpaper.Styles[styleDrop.SelectedIndex];
                Changed(false);
            };

            startupToggle = new ToggleSwitch { Text = "Windows の起動時に開始" };
            startupToggle.CheckedChanged += delegate
            {
                if (updatingStartup) return;
                // 自分からオンにしたときは、Windows の設定に残っている「オフ」も解除する
                try { Startup.SetEnabled(startupToggle.Checked, true); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "自動起動の設定に失敗しました。\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                RefreshStartupState();
            };

            // Windows の設定（スタートアップ アプリ）でオフにされているときの案内
            startupWarning = new FlatButton
            {
                Text = "Windows の設定で自動起動がオフになっています（クリックで設定を開く）",
                Glyph = Glyphs.Warning, Kind = ButtonKind.Subtle, Font = Theme.Small,
                TextColorOverride = Theme.Danger, Visible = false
            };
            startupWarning.Click += delegate { Startup.OpenWindowsStartupSettings(); };
            tips.SetToolTip(startupWarning, "「Windows の起動時に開始」のスイッチをオンにしても解除できます");
            // Windows の設定画面から戻ってきたときに、状態を読み直す
            Activated += delegate { RefreshStartupState(); };

            aboutButton = new FlatButton { Glyph = AppInfo.HasSupport ? Glyphs.Heart : Glyphs.Info, Kind = ButtonKind.Subtle };
            aboutButton.Click += delegate { AboutDialog.ShowFor(this); };
            tips.SetToolTip(header.ShuffleButton, "別の画像にする（フォルダの画像を一巡するまで、同じ画像は出ません）");
            tips.SetToolTip(aboutButton, AppInfo.HasSupport ? "このアプリについて・開発の支援" : "このアプリについて");

            Controls.AddRange(new Control[] { header, sectionTitle, countLabel, addButton, cardPanel, styleLabel, styleDrop, startupToggle, startupWarning, aboutButton });
            RefreshStartupState();

            EnableDrop(this);
            EnableDrop(cardPanel);
            EnableDrop(header);

            clock = new Timer { Interval = 20000 };
            clock.Tick += delegate { RefreshStatus(); };
            clock.Start();

            RebuildCards();
            DoLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindow(this);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            clock.Stop();
            clock.Dispose();
            tips.Dispose();
            base.OnFormClosed(e);
        }

        void DoLayout()
        {
            if (header == null || startupWarning == null) return;
            int pad = Theme.S(24), w = ClientSize.Width - pad * 2, btnH = Theme.S(34);
            header.SetBounds(pad, pad, w, Theme.S(222));

            int y = header.Bottom + Theme.S(28);
            addButton.SetBounds(pad + w - addButton.PreferredWidth(), y, addButton.PreferredWidth(), btnH);
            sectionTitle.Location = new Point(pad, y + (btnH - sectionTitle.Height) / 2);
            countLabel.Location = new Point(sectionTitle.Right + Theme.S(4), sectionTitle.Bottom - countLabel.Height - Theme.S(2));
            y += btnH + Theme.S(14);

            int footerY = ClientSize.Height - pad - btnH;
            int listBottom = footerY - Theme.S(20);
            if (showStartupWarning)
            {
                int ww = Math.Min(w, startupWarning.PreferredWidth());
                int wh = Theme.S(30);
                startupWarning.SetBounds(pad + w - ww, footerY - Theme.S(8) - wh, ww, wh);
                listBottom = startupWarning.Top - Theme.S(10);
            }
            cardPanel.SetBounds(pad, y, w, Math.Max(Theme.S(60), listBottom - y));

            styleLabel.Location = new Point(pad, footerY + (btnH - styleLabel.Height) / 2);
            styleDrop.SetBounds(styleLabel.Right + Theme.S(12), footerY, Theme.S(210), btnH);
            aboutButton.SetBounds(pad + w - btnH, footerY, btnH, btnH);
            startupToggle.FitWidth();
            startupToggle.SetBounds(aboutButton.Left - Theme.S(16) - startupToggle.Width, footerY, startupToggle.Width, btnH);
            FitCards();
        }

        /// 自動起動のスイッチと案内を、登録の有無と Windows の設定の両方から決める
        /// ・登録あり、Windows でもオン → スイッチ オン
        /// ・登録あり、Windows でオフ   → スイッチ オフ ＋ 案内を表示
        /// ・登録なし                   → スイッチ オフ
        void RefreshStartupState()
        {
            if (startupToggle == null || startupWarning == null) return;
            bool registered = false, blocked = false;
            try { registered = Startup.IsEnabled(); blocked = registered && Startup.IsBlockedByWindows(); } catch { }
            updatingStartup = true;
            startupToggle.Checked = registered && !blocked;
            updatingStartup = false;
            if (showStartupWarning != blocked)
            {
                showStartupWarning = blocked;
                startupWarning.Visible = blocked;
                DoLayout();
            }
        }

        void FitCards()
        {
            int cw = cardPanel.ClientSize.Width;
            foreach (Control c in cardPanel.Controls)
                if (c.Width != cw) c.Width = cw;
        }

        // ---- 表示の更新 ----
        void RebuildCards()
        {
            cardPanel.SuspendLayout();
            var old = cardPanel.Controls.Cast<Control>().ToList();
            cardPanel.Controls.Clear();
            foreach (var c in old) c.Dispose();
            foreach (var en in Scheduler.Sorted(settings.Entries))
            {
                var entry = en;
                var card = new ScheduleCard(entry);
                // カード自身のイベント中に自分を破棄しないよう、処理は後回しにする
                card.EditClicked += delegate { BeginInvoke((MethodInvoker)delegate { EditEntry(entry); }); };
                card.DeleteClicked += delegate { BeginInvoke((MethodInvoker)delegate { DeleteEntry(entry); }); };
                tips.SetToolTip(card, entry.IsFolder ? entry.FolderPath : entry.ImagePath);
                EnableDrop(card);
                cardPanel.Controls.Add(card);
            }
            cardPanel.ResumeLayout();
            FitCards();
            countLabel.Text = settings.Entries.Count > 0 ? settings.Entries.Count + " 件" : "";
            cardPanel.Invalidate();
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            var now = DateTime.Now;
            var active = Scheduler.GetActive(settings.Entries, now);
            header.Active = active;
            header.Next = Scheduler.GetNext(settings.Entries, now);
            header.Paused = settings.Paused;
            bool canShuffle = active != null && active.IsFolder && FolderImages.Count(active.FolderPath) > 1;
            if (header.CanShuffle != canShuffle) { header.CanShuffle = canShuffle; header.PerformLayout(); }
            header.Timeline.Entries = Scheduler.Sorted(settings.Entries);

            updating = true;
            header.AutoToggle.Checked = !settings.Paused;
            styleDrop.SelectedIndex = Math.Max(0, Array.IndexOf(Wallpaper.Styles, settings.Style));
            updating = false;

            header.Invalidate();
            header.Timeline.Invalidate();
            foreach (Control c in cardPanel.Controls)
            {
                var card = c as ScheduleCard;
                if (card == null) continue;
                bool isActive = card.Entry == active;
                card.IsActive = isActive;
                card.Invalidate(); // フォルダ指定の画像が選び直されたときも描き直す
            }
        }

        void PaintEmptyState(object sender, PaintEventArgs e)
        {
            if (cardPanel.Controls.Count > 0) return;
            int w = cardPanel.ClientSize.Width, cy = cardPanel.ClientSize.Height / 2;
            Theme.DrawText(e.Graphics, Glyphs.Photo, Theme.IconLarge, Theme.FgDim, new Rectangle(0, cy - Theme.S(70), w, Theme.S(50)), Theme.Center);
            Theme.DrawText(e.Graphics, "まだスケジュールがありません", Theme.BodySemi, Theme.Fg, new Rectangle(0, cy - Theme.S(12), w, Theme.S(26)), Theme.Center);
            Theme.DrawText(e.Graphics, "「追加」を押すか、画像やフォルダをここにドラッグして登録できます", Theme.Small, Theme.FgDim,
                new Rectangle(0, cy + Theme.S(16), w, Theme.S(22)), Theme.Center);
        }

        void Changed(bool rebuild)
        {
            if (rebuild) RebuildCards(); else RefreshStatus();
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        // ---- 追加・編集・削除 ----
        void AddEntry(string path)
        {
            var draft = new ScheduleEntry { Time = SuggestTime() };
            if (path != null && Directory.Exists(path)) draft.FolderPath = path; else draft.ImagePath = path;
            using (var dlg = new EntryDialog(draft, true))
                if (dlg.ShowDialog(this) == DialogResult.OK) Upsert(null, dlg.Result);
        }

        void EditEntry(ScheduleEntry entry)
        {
            using (var dlg = new EntryDialog(entry, false))
                if (dlg.ShowDialog(this) == DialogResult.OK) Upsert(entry, dlg.Result);
        }

        void DeleteEntry(ScheduleEntry entry)
        {
            string msg = entry.Time + " の切り替えを削除しますか？\n" + entry.DisplayName;
            using (var dlg = new ConfirmDialog("切り替えの削除", msg, "削除"))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                settings.Entries.Remove(entry);
                Changed(true);
            }
        }

        void Upsert(ScheduleEntry original, ScheduleEntry result)
        {
            if (original != null) settings.Entries.Remove(original);
            settings.Entries.RemoveAll(x => x.Time == result.Time);
            settings.Entries.Add(result);
            Changed(true);
        }

        string SuggestTime()
        {
            if (settings.Entries.Count == 0) return "07:00";
            var t = settings.Entries.Max(x => x.TimeOfDay).Add(TimeSpan.FromHours(1));
            if (t.TotalHours >= 24) t = t.Subtract(TimeSpan.FromHours(24));
            return ScheduleEntry.Format(t);
        }

        // ---- ドラッグ＆ドロップで追加 ----
        void EnableDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += OnDragEnter;
            c.DragDrop += OnDragDrop;
        }

        static string DroppedImage(DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            return files != null && files.Length > 0 && (ThumbCache.IsImageFile(files[0]) || Directory.Exists(files[0])) ? files[0] : null;
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DroppedImage(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            string file = DroppedImage(e);
            if (file != null) BeginInvoke((MethodInvoker)delegate { Activate(); AddEntry(file); });
        }
    }
}
