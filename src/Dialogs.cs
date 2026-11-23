// 追加・編集ダイアログと確認ダイアログ
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace WallpaperScheduler
{
    class ThemedDialog : Form
    {
        protected readonly FooterBand Footer;

        public ThemedDialog(string title, int width, int height)
        {
            Text = title;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            ClientSize = new Size(Theme.S(width), Theme.S(height));
            Footer = new FooterBand { Dock = DockStyle.Bottom };
            Controls.Add(Footer);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyWindow(this);
        }
    }

    class EntryDialog : ThemedDialog
    {
        readonly TimeSelector time;
        readonly ImageDropZone zone;
        readonly FlatButton modeButton, shuffleButton;
        readonly ToolTip tipsForMode;
        readonly ScheduleEntry original;

        public ScheduleEntry Result { get; private set; }

        public EntryDialog(ScheduleEntry source, bool isNew)
            : base(isNew ? "切り替えの追加" : "切り替えの編集", 700, 404)
        {
            original = source;
            int pad = Theme.S(24);

            var timeLabel = Theme.MakeLabel("時刻", Theme.BodySemi, Theme.Fg, Theme.Bg);
            timeLabel.Location = new Point(pad, pad);
            time = new TimeSelector { Location = new Point(pad, pad + Theme.S(36)) };
            TimeSpan ts;
            if (!TimeSpan.TryParseExact(source.Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out ts)) ts = new TimeSpan(7, 0, 0);
            time.Value = ts;
            Controls.Add(timeLabel);
            Controls.Add(time);

            // よく使う時刻のショートカット
            string[] names = { "朝", "昼", "夕方", "夜" };
            int[] hours = { 6, 12, 18, 21 };
            int chipW = (time.Width - Theme.S(8)) / 2, chipH = Theme.S(32);
            for (int i = 0; i < names.Length; i++)
            {
                int h = hours[i];
                var chip = new FlatButton { Text = names[i] + "  " + h + ":00", Kind = ButtonKind.Standard, Font = Theme.Small };
                chip.SetBounds(pad + (i % 2) * (chipW + Theme.S(8)), time.Bottom + Theme.S(18) + (i / 2) * (chipH + Theme.S(8)), chipW, chipH);
                chip.Click += delegate { time.Value = new TimeSpan(h, 0, 0); time.Focus(); };
                Controls.Add(chip);
            }

            int zx = pad + time.Width + Theme.S(36);
            var imageLabel = Theme.MakeLabel("画像", Theme.BodySemi, Theme.Fg, Theme.Bg);
            imageLabel.Location = new Point(zx, pad);
            zone = new ImageDropZone();
            if (source.IsFolder)
            {
                zone.FolderPath = source.FolderPath;
                if (File.Exists(source.ImagePath)) zone.PreviewImage = source.ImagePath;
            }
            else zone.ImagePath = source.ImagePath;
            int zw = ClientSize.Width - zx - pad;
            zone.SetBounds(zx, pad + Theme.S(36), zw, zw * 9 / 16);
            Controls.Add(imageLabel);
            Controls.Add(zone);

            // 「画像を選ぶ」⇔「フォルダを選ぶ」の切り替え（今と反対の選び方を出す）
            modeButton = new FlatButton { Kind = ButtonKind.Subtle, Font = Theme.Small };
            shuffleButton = new FlatButton { Kind = ButtonKind.Subtle, Font = Theme.Small, Text = "別の画像にする", Glyph = Glyphs.Shuffle };
            shuffleButton.Click += delegate { zone.ShufflePreview(); };
            Action updateMode = delegate
            {
                bool folder = zone.IsFolder;
                modeButton.Text = folder ? "1 枚の画像を選ぶ" : "フォルダから選ぶ";
                modeButton.Glyph = folder ? Glyphs.Photo : Glyphs.Folder;
                int bw = modeButton.PreferredWidth(), bh = Theme.S(30);
                modeButton.SetBounds(zone.Right - bw + Theme.S(8), pad + (imageLabel.Height - bh) / 2, bw, bh);
                imageLabel.Text = folder ? "フォルダ" : "画像";
                // 最初に表示する画像が気に入らないときに選び直すボタン（フォルダに 2 枚以上あるとき）
                shuffleButton.Visible = folder && FolderImages.Count(zone.FolderPath) > 1;
                int sw = shuffleButton.PreferredWidth();
                shuffleButton.SetBounds(modeButton.Left - sw, modeButton.Top, sw, bh);
                tipsForMode.SetToolTip(modeButton, folder
                    ? "フォルダではなく、決まった 1 枚の画像を表示します"
                    : "時間帯が始まるたびに、フォルダの中から画像をランダムに選んで表示します");
            };
            tipsForMode = new ToolTip();
            tipsForMode.SetToolTip(shuffleButton, "最初に表示する画像を選び直します。フォルダの画像を一巡するまで、同じ画像は出ません");
            modeButton.Click += delegate { if (zone.IsFolder) zone.ChooseImage(); else zone.ChooseFolder(); };
            zone.PathChanged += delegate { updateMode(); };
            updateMode();
            Controls.Add(modeButton);
            Controls.Add(shuffleButton);
            FormClosed += delegate { tipsForMode.Dispose(); };

            var save = Footer.Add(isNew ? "追加" : "保存", ButtonKind.Primary, DialogResult.None);
            var cancel = Footer.Add("キャンセル", ButtonKind.Standard, DialogResult.Cancel);
            save.Click += delegate { Accept(); };
            AcceptButton = save;
            CancelButton = cancel;

            Shown += delegate { time.Focus(); };
        }

        void Accept()
        {
            if (zone.IsFolder)
            {
                if (FolderImages.Count(zone.FolderPath) <= 0)
                {
                    zone.ShowError = true;
                    return;
                }
                string folder = Path.GetFullPath(zone.FolderPath);
                // 同じフォルダのまま編集したときは、今の画像と選んだ時間帯を引き継ぐ（保存しただけで変わらないように）
                // 画像を選び直したとき（「別の画像にする」）は、保存した時点でその画像を表示する
                bool sameFolder = original.IsFolder &&
                    string.Equals(Path.GetFullPath(original.FolderPath), folder, StringComparison.OrdinalIgnoreCase);
                bool sameImage = sameFolder && string.Equals(original.ImagePath, zone.PreviewImage, StringComparison.OrdinalIgnoreCase);
                string t = ScheduleEntry.Format(time.Value);
                Result = new ScheduleEntry
                {
                    Time = t,
                    FolderPath = folder,
                    ImagePath = zone.PreviewImage ?? FolderImages.Pick(folder, null),
                    PickedAt = sameImage && t == original.Time ? original.PickedAt : null,
                    // 同じフォルダなら「表示済み」の記録も引き継ぐ（一巡するまで同じ画像を出さないため）
                    Shown = sameFolder && original.Shown != null ? new List<string>(original.Shown) : null
                };
                DialogResult = DialogResult.OK;
                return;
            }
            string path = (zone.ImagePath ?? "").Trim().Trim('"');
            if (!File.Exists(path))
            {
                zone.ShowError = true;
                return;
            }
            Result = new ScheduleEntry { Time = ScheduleEntry.Format(time.Value), ImagePath = Path.GetFullPath(path) };
            DialogResult = DialogResult.OK;
        }
    }

    class AboutDialog : ThemedDialog
    {
        readonly Icon bigIcon;

        public AboutDialog() : base("このアプリについて", 520, AppInfo.HasSupport ? 286 : 250)
        {
            bigIcon = Theme.LoadAppIcon(new Size(Theme.S(72), Theme.S(72)));
            if (AppInfo.HasSupport)
            {
                var support = Footer.Add("開発を支援する", ButtonKind.Primary, DialogResult.None, Glyphs.Heart);
                support.Click += delegate { AppInfo.ShowSupportMenu(support); };
            }
            if (AppInfo.HasWebsite)
            {
                var web = Footer.Add("公式ページ", ButtonKind.Standard, DialogResult.None, Glyphs.OpenInNew);
                web.Click += delegate { AppInfo.Open(AppInfo.WebsiteUrl); };
            }
            var close = Footer.Add("閉じる", ButtonKind.Standard, DialogResult.Cancel);
            AcceptButton = close;
            CancelButton = close;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            int pad = Theme.S(24), iconSize = Theme.S(72);
            g.DrawIcon(bigIcon, new Rectangle(pad, pad, iconSize, iconSize));

            int x = pad + iconSize + Theme.S(20), w = ClientSize.Width - x - pad, y = pad - Theme.S(2);
            Theme.DrawText(g, "壁紙スケジューラ", Theme.Title, Theme.Fg, new Rectangle(x, y, w, Theme.S(36)), Theme.Left);
            y += Theme.S(38);
            Theme.DrawText(g, "バージョン " + AppInfo.Version, Theme.Body, Theme.FgDim, new Rectangle(x, y, w, Theme.S(22)), Theme.Left);
            y += Theme.S(32);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top;
            TextRenderer.DrawText(g, "指定した時刻に Windows の壁紙を自動で切り替えるアプリです。",
                Theme.Body, new Rectangle(x, y, w, Theme.S(48)), Theme.Fg, flags);
            if (AppInfo.HasSupport)
            {
                y += Theme.S(34);
                TextRenderer.DrawText(g, "気に入っていただけたら、開発を支援していただけるとうれしいです。",
                    Theme.Small, new Rectangle(x, y, w, Theme.S(44)), Theme.FgDim, flags);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            bigIcon.Dispose();
            base.OnFormClosed(e);
        }

        /// 親ウィンドウがあればその中央、なければ画面中央に表示
        public static void ShowFor(IWin32Window owner)
        {
            using (var d = new AboutDialog())
            {
                if (owner == null) d.StartPosition = FormStartPosition.CenterScreen;
                d.ShowDialog(owner);
            }
        }
    }

    class ConfirmDialog : ThemedDialog
    {
        readonly string message;

        public ConfirmDialog(string title, string message, string okText) : base(title, 420, 196)
        {
            this.message = message;
            Footer.Add(okText, ButtonKind.Danger, DialogResult.OK);
            var cancel = Footer.Add("キャンセル", ButtonKind.Standard, DialogResult.Cancel);
            AcceptButton = cancel;
            CancelButton = cancel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int pad = Theme.S(24);
            var r = new Rectangle(pad, pad, ClientSize.Width - pad * 2, ClientSize.Height - Footer.Height - pad * 2);
            TextRenderer.DrawText(e.Graphics, message, Theme.Body, r, Theme.Fg,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top);
        }
    }
}
