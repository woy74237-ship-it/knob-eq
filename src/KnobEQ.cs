// KnobEQ - Equalizer APO 用のつまみ式イコライザ (Sub / Bass / Low-Mid / Mid / High-Mid / Treble)
// つまみを回すと Equalizer APO の config フォルダ内の knobeq.txt を書き換え、即座に反映される。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

class Knob : Control
{
    public const float Min = -12f, Max = 12f;
    float value;
    int dragY; float dragStart; bool dragging;
    public event EventHandler ValueChanged;

    public Knob()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.SizeNS;
    }

    public float Value
    {
        get { return value; }
        set
        {
            float v = Math.Max(Min, Math.Min(Max, value));
            v = (float)Math.Round(v * 10f) / 10f;
            if (v == this.value) return;
            this.value = v;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { dragging = true; dragY = e.Y; dragStart = value; Capture = true; }
        Focus();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging) return;
        float step = (ModifierKeys & Keys.Shift) != 0 ? 0.02f : 0.1f; // Shift で微調整
        Value = dragStart + (dragY - e.Y) * step;
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; }
    protected override void OnMouseWheel(MouseEventArgs e) { Value = value + (e.Delta > 0 ? 0.5f : -0.5f); }
    protected override void OnDoubleClick(EventArgs e) { Value = 0f; } // ダブルクリックで 0 に戻す

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float size = Math.Min(Width, Height) - 6;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        using (Pen p = new Pen(Color.FromArgb(120, 165, 190), 3f))
            g.DrawEllipse(p, cx - r, cy - r, size, size);
        double t = (value - Min) / (Max - Min);
        double rad = (-135 + 270 * t) * Math.PI / 180.0;
        float rr = r * 0.70f, dot = Math.Max(8f, size * 0.13f);
        float x = cx + (float)Math.Sin(rad) * rr, y = cy - (float)Math.Cos(rad) * rr;
        g.FillEllipse(Brushes.White, x - dot / 2, y - dot / 2, dot, dot);
    }
}

// 1 つのつまみが担当するフィルター
class Band
{
    public string Name, Filter; // Filter は Equalizer APO の書式 ("LS", "PK", "HS")
    public int Freq; public float Q;
    public bool InThree; // 3 ノブモードでも表示するか
    public Knob Knob; public Label NameLabel, ValueLabel;
    public Band(string name, string filter, int freq, float q, bool inThree)
    { Name = name; Filter = filter; Freq = freq; Q = q; InThree = inThree; }
}

// 全バンドを合わせた周波数特性（どれだけ持ち上げ／下げているか）を描くグラフ
class ResponseGraph : Control
{
    const double Fs = 48000;            // 計算に使うサンプリング周波数
    const double FMin = 20, FMax = 20000;
    public Band[] Bands;
    public Func<Band, bool> IsActive;
    public bool On = true;

    static readonly Color Panel = Color.FromArgb(36, 48, 61);
    static readonly Color Grid = Color.FromArgb(62, 80, 98);
    static readonly Color Text_ = Color.FromArgb(120, 150, 170);
    static readonly Color Curve = Color.FromArgb(150, 205, 230);

    public ResponseGraph()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    // RBJ Audio EQ Cookbook の式で 1 つのフィルターのゲイン (dB) を求める
    static double BandDb(Band b, double gainDb, double f)
    {
        if (gainDb == 0) return 0;
        double A = Math.Pow(10, gainDb / 40), w0 = 2 * Math.PI * b.Freq / Fs;
        double c = Math.Cos(w0), s = Math.Sin(w0);
        double b0, b1, b2, a0, a1, a2;
        if (b.Filter == "PK")
        {
            double al = s / (2 * b.Q);
            b0 = 1 + al * A; b1 = -2 * c; b2 = 1 - al * A;
            a0 = 1 + al / A; a1 = -2 * c; a2 = 1 - al / A;
        }
        else
        {
            // Equalizer APO の LS / HS は Q 0.707 相当の傾き
            double al = s / 2 * Math.Sqrt(2), sq = 2 * Math.Sqrt(A) * al;
            if (b.Filter == "LS")
            {
                b0 = A * ((A + 1) - (A - 1) * c + sq); b1 = 2 * A * ((A - 1) - (A + 1) * c); b2 = A * ((A + 1) - (A - 1) * c - sq);
                a0 = (A + 1) + (A - 1) * c + sq;       a1 = -2 * ((A - 1) + (A + 1) * c);    a2 = (A + 1) + (A - 1) * c - sq;
            }
            else
            {
                b0 = A * ((A + 1) + (A - 1) * c + sq); b1 = -2 * A * ((A - 1) + (A + 1) * c); b2 = A * ((A + 1) + (A - 1) * c - sq);
                a0 = (A + 1) - (A - 1) * c + sq;       a1 = 2 * ((A - 1) - (A + 1) * c);      a2 = (A + 1) - (A - 1) * c - sq;
            }
        }
        // H(e^jw) の大きさ
        double w = 2 * Math.PI * f / Fs, c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
        double nr = b0 + b1 * c1 + b2 * c2, ni = -(b1 * s1 + b2 * s2);
        double dr = a0 + a1 * c1 + a2 * c2, di = -(a1 * s1 + a2 * s2);
        return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di));
    }

    double TotalDb(double f)
    {
        if (!On || Bands == null) return 0;
        double sum = 0;
        foreach (Band b in Bands)
            if (b.Knob != null && (IsActive == null || IsActive(b))) sum += BandDb(b, b.Knob.Value, f);
        return sum;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Panel);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const int padL = 24, padR = 6, padT = 6, padB = 16;
        RectangleF area = new RectangleF(padL, padT, Width - padL - padR, Height - padT - padB);

        // 周波数ごとのゲインを計算し、縦軸の範囲を決める（±12 dB を基本に、超えたら 6 dB 刻みで広げる）
        int n = (int)area.Width + 1;
        double[] db = new double[n];
        double peak = 0;
        for (int i = 0; i < n; i++)
        {
            db[i] = TotalDb(FMin * Math.Pow(FMax / FMin, i / (double)(n - 1)));
            peak = Math.Max(peak, Math.Abs(db[i]));
        }
        double range = Math.Max(12, Math.Ceiling(peak / 6) * 6);

        Func<double, float> X = f => area.Left + (float)(Math.Log(f / FMin) / Math.Log(FMax / FMin)) * area.Width;
        Func<double, float> Y = d => area.Top + (float)((range - d) / (2 * range)) * area.Height;

        using (Font font = new Font("Segoe UI", 7f))
        using (Brush tb = new SolidBrush(Text_))
        using (Pen gp = new Pen(Grid, 1f))
        {
            StringFormat center = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            foreach (double f in new double[] { 100, 1000, 10000 })
            {
                float x = X(f);
                g.DrawLine(gp, x, area.Top, x, area.Bottom);
                g.DrawString(f >= 1000 ? (f / 1000) + "k" : f.ToString(), font, tb, x, area.Bottom + 1, center);
            }
            for (double d = -range; d <= range; d += range / 2)
            {
                float y = Y(d);
                using (Pen p = new Pen(d == 0 ? Text_ : Grid, 1f)) g.DrawLine(p, area.Left, y, area.Right, y);
                string label = d > 0 ? "+" + d : d.ToString();
                g.DrawString(label, font, tb, padL - 2, y, right);
            }
        }

        // 補正カーブ（0 dB との間を薄く塗る）
        PointF[] pts = new PointF[n];
        for (int i = 0; i < n; i++) pts[i] = new PointF(area.Left + i, Y(db[i]));
        using (GraphicsPath fill = new GraphicsPath())
        {
            fill.AddLine(area.Left, Y(0), area.Left, pts[0].Y);
            fill.AddLines(pts);
            fill.AddLine(area.Right, pts[n - 1].Y, area.Right, Y(0));
            fill.CloseFigure();
            using (Brush fb = new SolidBrush(Color.FromArgb(On ? 50 : 20, Curve))) g.FillPath(fb, fill);
        }
        using (Pen cp = new Pen(On ? Curve : Grid, 2f)) g.DrawLines(cp, pts);

        if (!On)
            using (Font f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Brush b = new SolidBrush(Text_))
                g.DrawString("EQ OFF", f, b, area.Left + area.Width / 2, area.Top + 4, new StringFormat { Alignment = StringAlignment.Center });
    }
}

class MainForm : Form
{
    static readonly Color Bg = Color.FromArgb(46, 61, 77);
    static readonly Color LabelColor = Color.FromArgb(150, 190, 210);
    static readonly Color ValueColor = Color.FromArgb(205, 218, 228);

    // 画面の上から順に並ぶ（高音 → 低音）
    Band[] bands = new Band[] {
        new Band("Treble",   "HS", 8000, 0f,   true),
        new Band("High-Mid", "PK", 3000, 1.0f, false),
        new Band("Mid",      "PK", 1000, 1.0f, true),
        new Band("Low-Mid",  "PK", 300,  1.0f, false),
        new Band("Bass",     "LS", 100,  0f,   true),
        new Band("Sub",      "PK", 40,   1.0f, false),
    };

    const int GraphTop = 45, GraphHeight = 130;
    const int KnobTop = GraphTop + GraphHeight + 15, Step = 105, KnobSize = 80;
    static readonly Color Accent = Color.FromArgb(120, 165, 190);

    CheckBox onBox;
    Button reset, mode3Btn, mode6Btn;
    ResponseGraph graph;
    bool threeMode; // true: 3 ノブ (Treble / Mid / Bass)、false: 6 ノブ
    Timer saveTimer = new Timer();
    string configDir, eqFile;
    bool loading;

    public MainForm()
    {
        Text = "Knob EQ";
        BackColor = Bg;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        AddLabel("EQUALIZER", 18, 10, 130, 11f, FontStyle.Bold, LabelColor, ContentAlignment.MiddleLeft);

        // 3 / 6 ノブの切り替えボタン（右上）
        ToolTip tip = new ToolTip();
        mode3Btn = AddButton("3", 166, 12, 28, 24);
        mode6Btn = AddButton("6", 196, 12, 28, 24);
        tip.SetToolTip(mode3Btn, "3 ノブ（Treble / Mid / Bass）");
        tip.SetToolTip(mode6Btn, "6 ノブ（全バンド）");
        mode3Btn.Click += delegate { SetMode(true); };
        mode6Btn.Click += delegate { SetMode(false); };

        // 補正カーブのグラフ（見出しの下）
        graph = new ResponseGraph();
        graph.Bounds = new Rectangle(12, GraphTop, 216, GraphHeight);
        graph.Bands = bands;
        graph.IsActive = IsActive;
        Controls.Add(graph);

        foreach (Band b in bands)
        {
            b.Knob = new Knob();
            b.Knob.Size = new Size(KnobSize, KnobSize);
            Controls.Add(b.Knob);

            string freq = b.Freq >= 1000 ? (b.Freq / 1000) + "k" : b.Freq.ToString();
            b.NameLabel = AddLabel(b.Name + "  " + freq, 0, 0, 105, 10f, FontStyle.Regular, LabelColor, ContentAlignment.MiddleCenter);
            b.ValueLabel = AddLabel("0.0", 0, 0, 105, 20f, FontStyle.Regular, ValueColor, ContentAlignment.MiddleCenter);

            tip.SetToolTip(b.Knob, "上下ドラッグ / ホイールで調整（Shift で微調整）\nダブルクリックで 0 dB");
            b.Knob.ValueChanged += delegate { UpdateLabels(); ScheduleSave(); };
        }

        onBox = new CheckBox();
        onBox.Text = "EQ ON";
        onBox.Checked = true;
        onBox.ForeColor = LabelColor;
        onBox.AutoSize = true;
        onBox.CheckedChanged += delegate { graph.On = onBox.Checked; graph.Invalidate(); ScheduleSave(); };
        Controls.Add(onBox);

        reset = AddButton("Reset", 0, 0, 75, 28);
        // 表示中のつまみだけ 0 に戻す（隠れているつまみの値は残す）
        reset.Click += delegate { foreach (Band b in bands) if (IsActive(b)) b.Knob.Value = 0; };

        saveTimer.Interval = 120;
        saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };

        configDir = FindConfigDir();
        eqFile = Path.Combine(configDir, "knobeq.txt");
        Load_();
        DoLayout();
        EnsureInclude();
        Save();
    }

    bool IsActive(Band b) { return !threeMode || b.InThree; }

    void SetMode(bool three)
    {
        if (three == threeMode) return;
        threeMode = three;
        DoLayout();
        ScheduleSave();
    }

    // 表示中のつまみを上から詰めて並べ、ウィンドウの高さを合わせる
    void DoLayout()
    {
        SuspendLayout();
        int row = 0;
        foreach (Band b in bands)
        {
            bool show = IsActive(b);
            b.Knob.Visible = b.NameLabel.Visible = b.ValueLabel.Visible = show;
            if (!show) continue;
            int y = KnobTop + row * Step;
            b.Knob.Location = new Point(25, y);            // つまみは左
            b.NameLabel.Location = new Point(120, y + 2);  // 名前と値は右
            b.ValueLabel.Location = new Point(120, y + 28);
            row++;
        }
        int bottom = KnobTop + row * Step;
        onBox.Location = new Point(20, bottom + 12);
        reset.Location = new Point(145, bottom + 7);
        ClientSize = new Size(240, bottom + 50);

        // 選択中のモードのボタンを塗りつぶして示す
        StyleModeButton(mode3Btn, threeMode);
        StyleModeButton(mode6Btn, !threeMode);
        graph.Invalidate();
        ResumeLayout();
    }

    void StyleModeButton(Button btn, bool selected)
    {
        btn.BackColor = selected ? Accent : Bg;
        btn.ForeColor = selected ? Bg : LabelColor;
    }

    Button AddButton(string text, int x, int y, int w, int h)
    {
        Button btn = new Button();
        btn.Text = text;
        btn.FlatStyle = FlatStyle.Flat;
        btn.ForeColor = LabelColor;
        btn.FlatAppearance.BorderColor = Accent;
        btn.Location = new Point(x, y);
        btn.Size = new Size(w, h);
        Controls.Add(btn);
        return btn;
    }

    Label AddLabel(string text, int x, int y, int w, float size, FontStyle style, Color color, ContentAlignment align)
    {
        Label l = new Label();
        l.Text = text;
        l.Location = new Point(x, y);
        l.Size = new Size(w, (int)(size * 1.9f));
        l.Font = new Font(size >= 20 ? "Segoe UI Light" : "Segoe UI", size, style);
        l.ForeColor = color;
        l.BackColor = Color.Transparent;
        l.TextAlign = align;
        Controls.Add(l);
        return l;
    }

    static string Fmt(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
    static float Parse(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    Band Find(string name) { foreach (Band b in bands) if (b.Name == name) return b; return null; }

    void UpdateLabels()
    {
        foreach (Band b in bands) b.ValueLabel.Text = Fmt(b.Knob.Value);
        if (graph != null) graph.Invalidate();
    }

    void ScheduleSave() { if (loading) return; saveTimer.Stop(); saveTimer.Start(); }

    static string FindConfigDir()
    {
        try
        {
            using (RegistryKey k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\EqualizerAPO"))
            {
                if (k != null)
                {
                    string p = k.GetValue("ConfigPath") as string;
                    if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
                }
            }
        }
        catch { }
        return @"C:\Program Files\EqualizerAPO\config";
    }

    // 前回の値を knobeq.txt の先頭コメントから復元
    //   書式: "# state: Treble=0.0 High-Mid=0.0 ... Bass=1.0 Sub=0.0 mode=6 on"
    void Load_()
    {
        loading = true;
        try
        {
            if (File.Exists(eqFile))
            {
                foreach (string line in File.ReadAllLines(eqFile))
                {
                    if (!line.StartsWith("# state:")) continue;
                    threeMode = false; // mode= が無い古い形式は 6 ノブ扱い
                    string[] p = line.Substring(8).Trim().Split(' ');
                    if (p.Length == 0) continue;
                    foreach (string kv in p)
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 0) continue;
                        if (kv.StartsWith("mode=")) { threeMode = kv == "mode=3"; continue; }
                        Band b = Find(kv.Substring(0, eq));
                        if (b != null) b.Knob.Value = Parse(kv.Substring(eq + 1));
                    }
                    onBox.Checked = p[p.Length - 1] != "off";
                }
            }
        }
        catch { }
        loading = false;
        UpdateLabels();
    }

    // config.txt に "Include: knobeq.txt" が無ければ追加（初回のみバックアップを作成）
    void EnsureInclude()
    {
        string cfg = Path.Combine(configDir, "config.txt");
        try
        {
            string[] lines = File.Exists(cfg) ? File.ReadAllLines(cfg) : new string[0];
            foreach (string l in lines)
                if (l.Trim().Equals("Include: knobeq.txt", StringComparison.OrdinalIgnoreCase)) return;

            string backup = cfg + ".knobeq-backup";
            if (File.Exists(cfg) && !File.Exists(backup)) File.Copy(cfg, backup);

            // peace.txt の直後に挿入（HeSuVi などより前に置く）。無ければ末尾に追加。
            StringBuilder sb = new StringBuilder();
            bool inserted = false;
            foreach (string l in lines)
            {
                sb.AppendLine(l);
                if (!inserted && l.Trim().Equals("Include: peace.txt", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("Include: knobeq.txt");
                    inserted = true;
                }
            }
            if (!inserted) { sb.AppendLine(); sb.AppendLine("Include: knobeq.txt"); }
            File.WriteAllText(cfg, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            MessageBox.Show("config.txt を更新できませんでした:\n" + ex.Message, Text);
        }
    }

    void Save()
    {
        bool on = onBox.Checked;
        StringBuilder state = new StringBuilder("# state:");
        float boost = 0f;
        foreach (Band b in bands)
        {
            state.Append(" " + b.Name + "=" + Fmt(b.Knob.Value)); // 隠れているつまみの値も覚えておく
            if (IsActive(b)) boost = Math.Max(boost, b.Knob.Value);
        }
        state.Append(threeMode ? " mode=3" : " mode=6");
        state.Append(on ? " on" : " off");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# KnobEQ が自動生成するファイルです（手で編集しても上書きされます）");
        sb.AppendLine(state.ToString());
        if (on)
        {
            sb.AppendLine("Device: all");
            sb.AppendLine("Channel: all");
            // ブーストした分だけ全体を下げて音割れ（クリップ）を防ぐ
            sb.AppendLine("Preamp: " + Fmt(-boost) + " dB");
            // 低音から順に書く
            for (int i = bands.Length - 1; i >= 0; i--)
            {
                Band b = bands[i];
                if (!IsActive(b)) continue; // 3 ノブモードでは隠れているつまみを効かせない
                string line = "Filter: ON " + b.Filter + " Fc " + b.Freq + " Hz Gain " + Fmt(b.Knob.Value) + " dB";
                if (b.Filter == "PK") line += " Q " + Fmt(b.Q);
                sb.AppendLine(line);
            }
        }
        try { File.WriteAllText(eqFile, sb.ToString(), new UTF8Encoding(false)); }
        catch (Exception ex) { Text = "Knob EQ (保存エラー)"; Console.Error.WriteLine(ex.Message); }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
    }
}
