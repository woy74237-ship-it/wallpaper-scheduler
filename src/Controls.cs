// 自前描画のコントロール群（ボタン・トグル・ドロップダウン・タイムライン・カード・時刻選択・画像ドロップ）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WallpaperScheduler
{
    class SmoothControl : Control
    {
        protected bool Hover, Pressed;
        protected Point MousePos = new Point(-1, -1);

        public SmoothControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected Graphics Begin(PaintEventArgs e, Color background)
        {
            var g = e.Graphics;
            g.Clear(background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            return g;
        }

        protected Color ParentBack { get { return Parent != null ? Parent.BackColor : Theme.Bg; } }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; MousePos = new Point(-1, -1); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseMove(MouseEventArgs e) { MousePos = e.Location; base.OnMouseMove(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    }

    // ------------------------------------------------------------------
    enum ButtonKind { Standard, Primary, Subtle, Danger }

    class FlatButton : SmoothControl, IButtonControl
    {
        public string Glyph { get; set; }
        public ButtonKind Kind { get; set; }
        public DialogResult DialogResult { get; set; }
        /// 文字色を指定したいとき（Subtle・Standard のみ。空なら通常の色）
        public Color TextColorOverride { get; set; }

        public FlatButton()
        {
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            Size = new Size(Theme.S(110), Theme.S(34));
        }

        public void NotifyDefault(bool value) { }
        public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (DialogResult != DialogResult.None)
            {
                var f = FindForm();
                if (f != null) f.DialogResult = DialogResult;
            }
        }

        public int PreferredWidth()
        {
            int tw = Theme.Measure(Text, Font).Width;
            int gw = string.IsNullOrEmpty(Glyph) ? 0 : Theme.S(16);
            int gap = gw > 0 && tw > 0 ? Theme.S(8) : 0;
            return Math.Max(Theme.S(36), tw + gw + gap + Theme.S(36));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Theme.S(5);
            Color fg = Theme.Fg;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    Theme.FillRound(g, !Enabled ? Theme.Track : Pressed ? Theme.AccentPressed : Hover ? Theme.AccentHover : Theme.Accent, r, rad);
                    fg = Theme.OnAccent;
                    break;
                case ButtonKind.Danger:
                    Theme.FillRound(g, Pressed || Hover ? Theme.DangerHover : Theme.Danger, r, rad);
                    fg = Theme.OnDanger;
                    break;
                case ButtonKind.Standard:
                    Theme.FillRound(g, Theme.Surface, r, rad);
                    if (Hover) Theme.FillRound(g, Pressed ? Theme.PressOverlay : Theme.HoverOverlay, r, rad);
                    Theme.DrawRound(g, Theme.Border, r, rad, 1f);
                    break;
                case ButtonKind.Subtle:
                    if (Hover) Theme.FillRound(g, Pressed ? Theme.PressOverlay : Theme.HoverOverlay, r, rad);
                    break;
            }
            if (!TextColorOverride.IsEmpty && (Kind == ButtonKind.Subtle || Kind == ButtonKind.Standard)) fg = TextColorOverride;
            if (!Enabled) fg = Theme.FgDim;
            if (Pressed && Kind != ButtonKind.Primary && Kind != ButtonKind.Danger) fg = Theme.FgDim;

            int tw = Theme.Measure(Text, Font).Width;
            int gw = string.IsNullOrEmpty(Glyph) ? 0 : Theme.S(16);
            int gap = gw > 0 && tw > 0 ? Theme.S(8) : 0;
            int x = (Width - (gw + gap + tw)) / 2;
            if (gw > 0) Theme.DrawText(g, Glyph, Theme.IconFont, fg, new Rectangle(x, 0, gw, Height), Theme.Center);
            if (tw > 0) Theme.DrawText(g, Text, Font, fg, new Rectangle(x + gw + gap, 0, tw + 2, Height), Theme.Center);
        }
    }

    // ------------------------------------------------------------------
    class ToggleSwitch : SmoothControl
    {
        bool on;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            Height = Theme.S(34);
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public void FitWidth()
        {
            Width = Theme.S(40) + Theme.S(12) + Theme.Measure(Text, Font).Width + Theme.S(4);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            int tw = Theme.S(40), th = Theme.S(20), ty = (Height - th) / 2;
            var track = new RectangleF(0.5f, ty + 0.5f, tw - 1, th - 1);
            float kd = Theme.Sf(Hover ? 14 : 12);
            float cx;
            if (on)
            {
                Theme.FillRound(g, Hover ? Theme.AccentHover : Theme.Accent, track, th / 2f);
                cx = tw - th / 2f;
                using (var b = new SolidBrush(Theme.OnAccent))
                    g.FillEllipse(b, cx - kd / 2, ty + th / 2f - kd / 2, kd, kd);
            }
            else
            {
                if (Hover) Theme.FillRound(g, Theme.HoverOverlay, track, th / 2f);
                Theme.DrawRound(g, Theme.FgDim, track, th / 2f, Math.Max(1f, Theme.Scale));
                cx = th / 2f;
                using (var b = new SolidBrush(Theme.FgDim))
                    g.FillEllipse(b, cx - kd / 2, ty + th / 2f - kd / 2, kd, kd);
            }
            Theme.DrawText(g, Text, Font, Theme.Fg, new Rectangle(tw + Theme.S(12), 0, Width - tw - Theme.S(12), Height), Theme.Left);
        }
    }

    // ------------------------------------------------------------------
    class DropdownButton : SmoothControl
    {
        string[] items = new string[0];
        int selected = -1;
        public event EventHandler SelectedIndexChanged;

        public DropdownButton()
        {
            Cursor = Cursors.Hand;
            Font = Theme.Body;
        }

        public string[] Items { get { return items; } set { items = value ?? new string[0]; Invalidate(); } }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var menu = new ContextMenuStrip();
            Theme.StyleMenu(menu);
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                var item = Theme.MenuItem(items[i], delegate { SelectedIndex = index; });
                item.Checked = i == selected;
                menu.Items.Add(item);
            }
            menu.MinimumSize = new Size(Width, 0);
            menu.Closed += delegate { BeginInvoke((MethodInvoker)delegate { menu.Dispose(); }); };
            menu.Show(this, new Point(0, Height + Theme.S(4)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Theme.S(5);
            Theme.FillRound(g, Theme.Surface, r, rad);
            if (Hover) Theme.FillRound(g, Pressed ? Theme.PressOverlay : Theme.HoverOverlay, r, rad);
            Theme.DrawRound(g, Theme.Border, r, rad, 1f);
            string text = selected >= 0 && selected < items.Length ? items[selected] : "";
            int arrow = Theme.S(36);
            Theme.DrawText(g, text, Font, Theme.Fg, new Rectangle(Theme.S(12), 0, Width - Theme.S(12) - arrow, Height), Theme.Left);
            Theme.DrawText(g, Glyphs.ChevronDown, Theme.IconSmall, Theme.FgDim, new Rectangle(Width - arrow, 0, arrow - Theme.S(4), Height), Theme.Center);
        }
    }

    // ------------------------------------------------------------------
    /// 24 時間のどこでどの壁紙になるかを色の帯で表示
    class TimelineBar : SmoothControl
    {
        public List<ScheduleEntry> Entries = new List<ScheduleEntry>();

        public TimelineBar() { Height = Theme.S(46); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            float barH = Theme.S(12), top = Theme.S(6);
            var bar = new RectangleF(Theme.S(2), top, Width - Theme.S(4), barH);
            if (bar.Width < 10) return;
            Func<double, float> X = minutes => bar.X + (float)(bar.Width * minutes / 1440.0);

            using (var path = Theme.Round(bar, barH / 2f))
            {
                var list = Entries;
                if (list.Count == 0)
                {
                    using (var b = new SolidBrush(Theme.Track)) g.FillPath(b, path);
                }
                else
                {
                    // 色帯をいったんビットマップに描いてから角丸パスで塗る（縁をなめらかにするため）
                    int bw = (int)Math.Ceiling(bar.Width), bh = (int)Math.Ceiling(barH);
                    using (var bmp = new Bitmap(bw, bh))
                    {
                        using (var bg = Graphics.FromImage(bmp))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                double start = list[i].TimeOfDay.TotalMinutes;
                                double end = list[(i + 1) % list.Count].TimeOfDay.TotalMinutes;
                                var thumb = ThumbCache.Get(list[i].ImagePath);
                                using (var b = new SolidBrush(thumb != null ? thumb.Average : Theme.Track))
                                {
                                    if (list.Count == 1) bg.FillRectangle(b, 0, 0, bw, bh);
                                    else if (end > start) FillSpan(bg, b, start, end, bw, bh);
                                    else { FillSpan(bg, b, start, 1440, bw, bh); FillSpan(bg, b, 0, end, bw, bh); }
                                }
                            }
                            if (list.Count > 1)
                                using (var sep = new SolidBrush(BackColor))
                                    foreach (var en in list)
                                    {
                                        float sx = (float)(bw * en.TimeOfDay.TotalMinutes / 1440.0);
                                        bg.FillRectangle(sep, sx - Theme.Sf(1), 0, Theme.Sf(2), bh);
                                    }
                        }
                        using (var tb = new TextureBrush(bmp, WrapMode.Clamp))
                        {
                            tb.TranslateTransform(bar.X, bar.Y);
                            g.FillPath(tb, path);
                        }
                    }
                }
            }

            // 目盛り
            int labelY = (int)(top + barH + Theme.S(6));
            foreach (int h in new[] { 0, 6, 12, 18, 24 })
            {
                string t = h + ":00";
                var sz = Theme.Measure(t, Theme.Small);
                int x = (int)X(h * 60) - sz.Width / 2;
                x = Math.Max(0, Math.Min(Width - sz.Width, x));
                Theme.DrawText(g, t, Theme.Small, Theme.FgDim, new Rectangle(x, labelY, sz.Width + 2, sz.Height), Theme.Left);
            }

            // 現在時刻のマーカー
            float nx = X(DateTime.Now.TimeOfDay.TotalMinutes);
            var marker = new RectangleF(nx - Theme.Sf(2.5f), top - Theme.Sf(4), Theme.Sf(5), barH + Theme.Sf(8));
            using (var p = Theme.Round(marker, Theme.Sf(2.5f)))
            using (var pen = new Pen(BackColor, Theme.Sf(2)))
            using (var b = new SolidBrush(Theme.Fg))
            {
                g.DrawPath(pen, p);
                g.FillPath(b, p);
            }
        }

        static void FillSpan(Graphics g, Brush b, double start, double end, int w, int h)
        {
            float x1 = (float)(w * start / 1440.0), x2 = (float)(w * end / 1440.0);
            g.FillRectangle(b, x1, 0, x2 - x1, h);
        }
    }

    // ------------------------------------------------------------------
    /// スケジュール 1 件分のカード
    class ScheduleCard : SmoothControl
    {
        public readonly ScheduleEntry Entry;
        public bool IsActive;
        public event EventHandler EditClicked, DeleteClicked;
        int hot; // 0: なし 1: 編集 2: 削除

        public ScheduleCard(ScheduleEntry entry)
        {
            Entry = entry;
            Height = Theme.S(80);
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 0, Theme.S(8));
        }

        Rectangle DeleteRect { get { int s = Theme.S(36); return new Rectangle(Width - Theme.S(14) - s, (Height - s) / 2, s, s); } }
        Rectangle EditRect { get { var d = DeleteRect; return new Rectangle(d.X - d.Width - Theme.S(4), d.Y, d.Width, d.Height); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = DeleteRect.Contains(e.Location) ? 2 : EditRect.Contains(e.Location) ? 1 : 0;
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hot = 0; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (DeleteRect.Contains(e.Location)) { if (DeleteClicked != null) DeleteClicked(this, EventArgs.Empty); }
            else if (EditClicked != null) EditClicked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Theme.S(8);
            Theme.FillRound(g, Theme.Surface, r, rad);
            if (Hover && hot == 0) Theme.FillRound(g, Pressed ? Theme.PressOverlay : Theme.HoverOverlay, r, rad);
            Theme.DrawRound(g, IsActive ? Theme.Blend(Theme.Accent, Theme.Surface, 0.4f) : Theme.Border, r, rad, 1f);

            if (IsActive)
                Theme.FillRound(g, Theme.Accent, new RectangleF(Theme.Sf(4), Height / 2f - Theme.Sf(14), Theme.Sf(3), Theme.Sf(28)), Theme.Sf(1.5f));

            // サムネイル
            var tr = new RectangleF(Theme.S(14), (Height - Theme.S(56)) / 2f, Theme.S(100), Theme.S(56));
            // フォルダ指定なら枚数（-1: フォルダが無い）
            int count = Entry.IsFolder ? FolderImages.Count(Entry.FolderPath) : 0;
            bool exists = Entry.IsFolder ? count > 0 : File.Exists(Entry.ImagePath);
            var thumb = exists ? ThumbCache.Get(Entry.ImagePath) : null;
            if (thumb != null) Theme.DrawImageCover(g, thumb.Image, tr, Theme.S(4));
            else
            {
                Theme.FillRound(g, Theme.Track, tr, Theme.S(4));
                Theme.DrawText(g, exists ? Glyphs.Folder : Glyphs.Warning, Theme.IconFont, Theme.FgDim, Rectangle.Round(tr), Theme.Center);
            }
            if (Entry.IsFolder && count > 0)
            {
                // サムネイルの右下に「フォルダのマーク＋枚数」
                string num = count.ToString();
                var ns = Theme.Measure(num, Theme.Small);
                int gw = Theme.S(14), bh = Theme.S(20), bw = Theme.S(6) + gw + Theme.S(3) + ns.Width + Theme.S(6);
                var br = new RectangleF(tr.Right - bw - Theme.S(4), tr.Bottom - bh - Theme.S(4), bw, bh);
                Theme.FillRound(g, Color.FromArgb(170, 0, 0, 0), br, Theme.S(4));
                Theme.DrawText(g, Glyphs.Folder, Theme.IconSmall, Color.White,
                    new Rectangle((int)br.X + Theme.S(6), (int)br.Y, gw, bh), Theme.Center);
                Theme.DrawText(g, num, Theme.Small, Color.White,
                    new Rectangle((int)br.X + Theme.S(6) + gw + Theme.S(3), (int)br.Y, ns.Width + 2, bh), Theme.Left);
            }

            // 時刻・バッジ・ファイル名
            int x = (int)tr.Right + Theme.S(18);
            int right = EditRect.X - Theme.S(8);
            var ts = Theme.Measure(Entry.Time, Theme.TimeFont);
            int timeY = Theme.S(10);
            Theme.DrawText(g, Entry.Time, Theme.TimeFont, Theme.Fg, new Rectangle(x, timeY, ts.Width + 2, Theme.S(34)), Theme.Left);
            if (IsActive)
            {
                string badge = "表示中";
                var bs = Theme.Measure(badge, Theme.Small);
                var br = new RectangleF(x + ts.Width + Theme.S(12), timeY + (Theme.S(34) - Theme.S(22)) / 2f, bs.Width + Theme.S(16), Theme.S(22));
                Theme.FillRound(g, Theme.Accent, br, br.Height / 2f);
                Theme.DrawText(g, badge, Theme.Small, Theme.OnAccent, Rectangle.Round(br), Theme.Center);
            }
            string name;
            if (!Entry.IsFolder)
                name = Entry.DisplayName + (exists ? "" : "  —  ファイルが見つかりません");
            else if (count < 0) name = Entry.DisplayName + "  —  フォルダが見つかりません";
            else if (count == 0) name = Entry.DisplayName + "  —  フォルダに画像がありません";
            else name = Entry.DisplayName + " フォルダからランダム  ·  " + Path.GetFileName(Entry.ImagePath);
            Theme.DrawText(g, name, Theme.Body, exists ? Theme.FgDim : Theme.Danger,
                new Rectangle(x, Theme.S(44), Math.Max(0, right - x), Theme.S(24)), Theme.Left);

            // 編集・削除ボタン
            DrawIconButton(g, EditRect, Glyphs.Edit, hot == 1, Theme.Fg);
            DrawIconButton(g, DeleteRect, Glyphs.Delete, hot == 2, Theme.Danger);
        }

        void DrawIconButton(Graphics g, Rectangle r, string glyph, bool isHot, Color hotColor)
        {
            if (isHot) Theme.FillRound(g, Pressed ? Theme.PressOverlay : Theme.HoverOverlay, r, Theme.S(5));
            Theme.DrawText(g, glyph, Theme.IconFont, isHot ? hotColor : Theme.FgDim, r, Theme.Center);
        }
    }

    // ------------------------------------------------------------------
    /// 時・分を大きく表示し、矢印・ホイール・キー入力で変更できる時刻選択
    class TimeSelector : SmoothControl
    {
        int hour = 7, minute, part;
        string buffer = "";
        public event EventHandler ValueChanged;

        public TimeSelector()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Size = new Size(Theme.S(180), Theme.S(136));
        }

        public TimeSpan Value
        {
            get { return new TimeSpan(hour, minute, 0); }
            set { hour = value.Hours; minute = value.Minutes; buffer = ""; Changed(); }
        }

        Rectangle Box(int p) { int bw = Theme.S(76); return new Rectangle(p == 0 ? 0 : Width - bw, Theme.S(36), bw, Theme.S(64)); }
        Rectangle Up(int p) { var b = Box(p); return new Rectangle(b.X, 0, b.Width, Theme.S(30)); }
        Rectangle Down(int p) { var b = Box(p); return new Rectangle(b.X, b.Bottom + Theme.S(6), b.Width, Theme.S(30)); }

        void Changed()
        {
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        void Step(int p, int dir, bool coarse)
        {
            if (p == 0) hour = (hour + dir + 24) % 24;
            else if (coarse)
            {
                int m = dir > 0 ? (minute / 5 + 1) * 5 : ((minute + 4) / 5 - 1) * 5;
                minute = ((m % 60) + 60) % 60;
            }
            else minute = (minute + dir + 60) % 60;
            buffer = "";
            Changed();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            for (int p = 0; p < 2; p++)
            {
                if (Up(p).Contains(e.Location)) { part = p; Step(p, 1, true); return; }
                if (Down(p).Contains(e.Location)) { part = p; Step(p, -1, true); return; }
                if (Box(p).Contains(e.Location)) { part = p; buffer = ""; Invalidate(); return; }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int p = e.X > Width / 2 ? 1 : 0;
            part = p;
            Step(p, e.Delta > 0 ? 1 : -1, true);
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Up: Step(part, 1, false); break;
                case Keys.Down: Step(part, -1, false); break;
                case Keys.Left: part = 0; buffer = ""; Invalidate(); break;
                case Keys.Right: part = 1; buffer = ""; Invalidate(); break;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (!char.IsDigit(e.KeyChar)) return;
            int max = part == 0 ? 23 : 59;
            int d = e.KeyChar - '0';
            if (buffer.Length == 0)
            {
                SetPart(d);
                // 2 桁目が来ても範囲外になる数字なら、その場で確定して次へ
                if (d * 10 > max) { buffer = ""; if (part == 0) part = 1; }
                else buffer = d.ToString();
            }
            else
            {
                SetPart(Math.Min(max, int.Parse(buffer) * 10 + d));
                buffer = "";
                if (part == 0) part = 1;
            }
            Changed();
            e.Handled = true;
        }

        void SetPart(int v) { if (part == 0) hour = v; else minute = v; }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); buffer = ""; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            for (int p = 0; p < 2; p++)
            {
                var b = Box(p);
                var rf = new RectangleF(b.X + 0.5f, b.Y + 0.5f, b.Width - 1, b.Height - 1);
                Theme.FillRound(g, Theme.Surface, rf, Theme.S(8));
                bool focused = Focused && part == p;
                if (focused)
                    Theme.DrawRound(g, Theme.Accent, new RectangleF(b.X + 1, b.Y + 1, b.Width - 2, b.Height - 2), Theme.S(8), Theme.Sf(2));
                else
                    Theme.DrawRound(g, Theme.Border, rf, Theme.S(8), 1f);
                string text = (p == 0 ? hour : minute).ToString("00");
                Theme.DrawText(g, text, Theme.TimeBig, Theme.Fg, b, Theme.Center);

                DrawArrow(g, Up(p), Glyphs.ChevronUp);
                DrawArrow(g, Down(p), Glyphs.ChevronDown);
            }
            var b0 = Box(0); var b1 = Box(1);
            Theme.DrawText(g, ":", Theme.TimeBig, Theme.FgDim, new Rectangle(b0.Right, b0.Y - Theme.S(4), b1.X - b0.Right, b0.Height), Theme.Center);
        }

        void DrawArrow(Graphics g, Rectangle r, string glyph)
        {
            bool hot = r.Contains(MousePos);
            if (hot) Theme.FillRound(g, Pressed ? Theme.PressOverlay : Theme.HoverOverlay, r, Theme.S(5));
            Theme.DrawText(g, glyph, Theme.IconSmall, hot ? Theme.Fg : Theme.FgDim, r, Theme.Center);
        }
    }

    // ------------------------------------------------------------------
    /// クリックで選択・ドラッグ＆ドロップで画像を指定するエリア
    class ImageDropZone : SmoothControl
    {
        string path, folder, preview;
        bool dragging, error;
        public event EventHandler PathChanged;

        public ImageDropZone()
        {
            AllowDrop = true;
            Cursor = Cursors.Hand;
        }

        /// 画像 1 枚を指定（フォルダ指定は解除される）
        public string ImagePath
        {
            get { return path; }
            set { path = value; folder = null; preview = null; Changed(); }
        }

        /// フォルダを指定（画像 1 枚の指定は解除される）
        public string FolderPath
        {
            get { return folder; }
            set
            {
                folder = value; path = null;
                previewSeen.Clear();
                preview = FolderImages.PickUnseen(value, null, previewSeen);
                Changed();
            }
        }

        // 見本の画像を選び直したときに、フォルダの画像を一巡するまで同じ画像を出さないための記録
        readonly List<string> previewSeen = new List<string>();

        /// 見本の画像を、別の画像に選び直す（最初に表示する画像になる）
        public void ShufflePreview()
        {
            if (!IsFolder) return;
            string next = FolderImages.PickUnseen(folder, preview, previewSeen);
            if (next == null) return;
            preview = next;
            Changed();
        }

        /// フォルダ指定のとき、見本として表示している画像（登録時の最初の画像にも使う）
        public string PreviewImage
        {
            get { return preview; }
            set { if (folder != null) { preview = value; previewSeen.Clear(); Invalidate(); } }
        }

        public bool IsFolder { get { return folder != null; } }

        void Changed()
        {
            error = false;
            Invalidate();
            if (PathChanged != null) PathChanged(this, EventArgs.Empty);
        }

        public bool ShowError { get { return error; } set { error = value; Invalidate(); } }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (IsFolder) ChooseFolder(); else ChooseImage();
        }

        public void ChooseImage()
        {
            using (var ofd = new OpenFileDialog { Filter = ThumbCache.Filter, Title = "壁紙にする画像を選択" })
            {
                try
                {
                    if (File.Exists(path)) ofd.InitialDirectory = Path.GetDirectoryName(path);
                    else if (Directory.Exists(folder)) ofd.InitialDirectory = folder;
                }
                catch { }
                if (ofd.ShowDialog(FindForm()) == DialogResult.OK) ImagePath = ofd.FileName;
            }
        }

        public void ChooseFolder()
        {
            string start = folder;
            try { if (start == null && File.Exists(path)) start = Path.GetDirectoryName(path); } catch { }
            string f = FolderPicker.Show(FindForm(), "壁紙にする画像が入ったフォルダを選択", start);
            if (f != null) FolderPath = f;
        }

        static string DroppedFile(DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1) return null;
            return ThumbCache.IsImageFile(files[0]) || Directory.Exists(files[0]) ? files[0] : null;
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = DroppedFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
            dragging = e.Effect != DragDropEffects.None;
            Invalidate();
        }

        protected override void OnDragLeave(EventArgs e) { base.OnDragLeave(e); dragging = false; Invalidate(); }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            dragging = false;
            string f = DroppedFile(e);
            if (f != null)
            {
                if (Directory.Exists(f)) FolderPath = f; else ImagePath = f;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Theme.S(8);
            int folderCount = IsFolder ? FolderImages.Count(folder) : 0;
            var thumb = ThumbCache.Get(IsFolder ? preview : path);

            if (thumb == null)
            {
                Theme.FillRound(g, Theme.Surface, r, rad);
                if (Hover || dragging) Theme.FillRound(g, Theme.HoverOverlay, r, rad);
                using (var pth = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), rad))
                using (var pen = new Pen(dragging ? Theme.Accent : error ? Theme.Danger : Theme.FgDim, Theme.Sf(dragging ? 2f : 1.2f)))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, pth);
                }
                int cy = Height / 2;
                Theme.DrawText(g, IsFolder ? Glyphs.Folder : Glyphs.Photo, Theme.IconLarge, dragging ? Theme.Accent : Theme.FgDim,
                    new Rectangle(0, cy - Theme.S(64), Width, Theme.S(48)), Theme.Center);
                string main = error ? "画像を選択してください" : "クリックして画像を選択";
                string sub = "またはここに画像やフォルダをドラッグ＆ドロップ";
                bool bad = error || !string.IsNullOrEmpty(path);
                if (IsFolder)
                {
                    main = folderCount < 0 ? "フォルダが見つかりません" : "フォルダに画像がありません";
                    sub = "クリックして別のフォルダを選択";
                    bad = true;
                }
                else if (!error && !string.IsNullOrEmpty(path)) main = "ファイルが見つかりません";
                Theme.DrawText(g, main, Theme.BodySemi, bad ? Theme.Danger : Theme.Fg,
                    new Rectangle(0, cy - Theme.S(4), Width, Theme.S(26)), Theme.Center);
                Theme.DrawText(g, sub, Theme.Small, Theme.FgDim,
                    new Rectangle(0, cy + Theme.S(22), Width, Theme.S(22)), Theme.Center);
                return;
            }

            Theme.DrawImageCover(g, thumb.Image, r, rad);
            // 下部にグラデーションを敷いてファイル名を表示
            var band = new RectangleF(0, Height - Theme.S(64), Width, Theme.S(64));
            using (var clip = Theme.Round(r, rad))
            using (var lg = new LinearGradientBrush(band, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(180, 0, 0, 0), 90f))
            {
                g.SetClip(clip);
                g.FillRectangle(lg, band.X, band.Y + 1, band.Width, band.Height);
                if (Hover) using (var ov = new SolidBrush(Color.FromArgb(30, 255, 255, 255))) g.FillRectangle(ov, r);
                g.ResetClip();
            }
            string hint = IsFolder ? folderCount + " 枚からランダムに表示" : "クリックして変更";
            var hs = Theme.Measure(hint, Theme.Small);
            int pad = Theme.S(14);
            var textRow = new Rectangle(pad, Height - Theme.S(40), Width - pad * 2, Theme.S(28));
            var nameRect = new Rectangle(textRow.X, textRow.Y, textRow.Width - hs.Width - Theme.S(12), textRow.Height);
            if (IsFolder)
            {
                int gw = Theme.S(22);
                Theme.DrawText(g, Glyphs.Folder, Theme.IconFont, Color.White, new Rectangle(nameRect.X, nameRect.Y, gw, nameRect.Height), Theme.Left);
                nameRect = new Rectangle(nameRect.X + gw, nameRect.Y, nameRect.Width - gw, nameRect.Height);
            }
            string label;
            try { label = IsFolder ? Path.GetFileName(folder.TrimEnd('\\', '/')) : Path.GetFileName(path); }
            catch { label = IsFolder ? folder : path; }
            Theme.DrawText(g, label, Theme.BodySemi, Color.White, nameRect, Theme.Left);
            Theme.DrawText(g, hint, Theme.Small, Color.FromArgb(220, 220, 220),
                new Rectangle(textRow.Right - hs.Width, textRow.Y, hs.Width + 2, textRow.Height), Theme.Left);
            if (dragging) Theme.DrawRound(g, Theme.Accent, new RectangleF(1, 1, Width - 3, Height - 3), rad, Theme.Sf(2));
        }
    }

    // ------------------------------------------------------------------
    /// ダイアログ下部のボタン帯
    class FooterBand : SmoothControl
    {
        public readonly List<FlatButton> Buttons = new List<FlatButton>();

        public FooterBand()
        {
            BackColor = Theme.Surface;
            Height = Theme.S(76);
        }

        public FlatButton Add(string text, ButtonKind kind, DialogResult result, string glyph = null)
        {
            var b = new FlatButton { Text = text, Kind = kind, DialogResult = result, Glyph = glyph };
            Buttons.Add(b);
            Controls.Add(b);
            PerformLayout();
            return b;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = Theme.S(24), gap = Theme.S(8), h = Theme.S(34);
            int total = 0;
            foreach (var b in Buttons) { b.Width = Math.Max(Theme.S(120), b.PreferredWidth()); total += b.Width + gap; }
            int x = Width - pad - (total - gap);
            foreach (var b in Buttons)
            {
                b.SetBounds(x, (Height - h) / 2, b.Width, h);
                x += b.Width + gap;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e, Theme.Surface);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, 0, Width, 0);
        }
    }

    // ------------------------------------------------------------------
    /// Windows 標準の「フォルダーの選択」画面（エクスプローラーと同じ見た目）。
    /// 使えない環境では、従来の FolderBrowserDialog で代用する
    static class FolderPicker
    {
        public static string Show(IWin32Window owner, string title, string initial)
        {
            try { return ShowModern(owner, title, initial); }
            catch (COMException) { }
            catch (InvalidCastException) { }
            using (var fbd = new FolderBrowserDialog { Description = title, ShowNewFolderButton = false })
            {
                try { if (Directory.Exists(initial)) fbd.SelectedPath = initial; } catch { }
                return fbd.ShowDialog(owner) == DialogResult.OK ? fbd.SelectedPath : null;
            }
        }

        static string ShowModern(IWin32Window owner, string title, string initial)
        {
            var dlg = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                uint opts;
                dlg.GetOptions(out opts);
                dlg.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
                dlg.SetTitle(title);
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                {
                    IShellItem item;
                    var iid = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initial, IntPtr.Zero, ref iid, out item) == 0)
                    {
                        dlg.SetFolder(item);
                        Marshal.ReleaseComObject(item);
                    }
                }
                int hr = dlg.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (hr != 0) return null; // キャンセル
                IShellItem result;
                dlg.GetResult(out result);
                try
                {
                    IntPtr p;
                    result.GetDisplayName(SIGDN_FILESYSPATH, out p);
                    try { return Marshal.PtrToStringUni(p); }
                    finally { Marshal.FreeCoTaskMem(p); }
                }
                finally { Marshal.ReleaseComObject(result); }
            }
            finally { Marshal.ReleaseComObject(dlg); }
        }

        const uint FOS_PICKFOLDERS = 0x20, FOS_FORCEFILESYSTEM = 0x40, FOS_PATHMUSTEXIST = 0x800;
        const uint SIGDN_FILESYSPATH = 0x80058000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        class FileOpenDialogRCW { }

        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr specs);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint fos);
            void GetOptions(out uint fos);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint sigdn, out IntPtr name);
            void GetAttributes(uint mask, out uint attribs);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
