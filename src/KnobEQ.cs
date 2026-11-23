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
    public Knob Knob; public Label ValueLabel;
    public Band(string name, string filter, int freq, float q) { Name = name; Filter = filter; Freq = freq; Q = q; }
}

class MainForm : Form
{
    static readonly Color Bg = Color.FromArgb(46, 61, 77);
    static readonly Color LabelColor = Color.FromArgb(150, 190, 210);
    static readonly Color ValueColor = Color.FromArgb(205, 218, 228);

    // 画面の上から順に並ぶ（高音 → 低音）
    Band[] bands = new Band[] {
        new Band("Treble",   "HS", 8000, 0f),
        new Band("High-Mid", "PK", 3000, 1.0f),
        new Band("Mid",      "PK", 1000, 1.0f),
        new Band("Low-Mid",  "PK", 300,  1.0f),
        new Band("Bass",     "LS", 100,  0f),
        new Band("Sub",      "PK", 40,   1.0f),
    };

    CheckBox onBox;
    Timer saveTimer = new Timer();
    string configDir, eqFile;
    bool loading;

    public MainForm()
    {
        const int top = 45, step = 105, knobSize = 80;
        Text = "Knob EQ";
        BackColor = Bg;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        int bottom = top + bands.Length * step;
        ClientSize = new Size(240, bottom + 50);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        AddLabel("EQUALIZER", 0, 10, 240, 11f, FontStyle.Bold, LabelColor, ContentAlignment.MiddleCenter);

        ToolTip tip = new ToolTip();
        for (int i = 0; i < bands.Length; i++)
        {
            Band b = bands[i];
            int y = top + i * step;
            const int knobX = 25, textX = 120; // つまみは左、名前と値は右に揃える

            b.Knob = new Knob();
            b.Knob.Location = new Point(knobX, y);
            b.Knob.Size = new Size(knobSize, knobSize);
            Controls.Add(b.Knob);

            string freq = b.Freq >= 1000 ? (b.Freq / 1000) + "k" : b.Freq.ToString();
            AddLabel(b.Name + "  " + freq, textX, y + 2, 105, 10f, FontStyle.Regular, LabelColor, ContentAlignment.MiddleCenter);
            b.ValueLabel = AddLabel("0.0", textX, y + 28, 105, 20f, FontStyle.Regular, ValueColor, ContentAlignment.MiddleCenter);

            tip.SetToolTip(b.Knob, "上下ドラッグ / ホイールで調整（Shift で微調整）\nダブルクリックで 0 dB");
            b.Knob.ValueChanged += delegate { UpdateLabels(); ScheduleSave(); };
        }

        onBox = new CheckBox();
        onBox.Text = "EQ ON";
        onBox.Checked = true;
        onBox.ForeColor = LabelColor;
        onBox.Location = new Point(20, bottom + 12);
        onBox.AutoSize = true;
        onBox.CheckedChanged += delegate { ScheduleSave(); };
        Controls.Add(onBox);

        Button reset = new Button();
        reset.Text = "Reset";
        reset.FlatStyle = FlatStyle.Flat;
        reset.ForeColor = LabelColor;
        reset.FlatAppearance.BorderColor = Color.FromArgb(120, 165, 190);
        reset.Location = new Point(145, bottom + 7);
        reset.Size = new Size(75, 28);
        reset.Click += delegate { foreach (Band b in bands) b.Knob.Value = 0; };
        Controls.Add(reset);

        saveTimer.Interval = 120;
        saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };

        configDir = FindConfigDir();
        eqFile = Path.Combine(configDir, "knobeq.txt");
        Load_();
        EnsureInclude();
        Save();
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

    void UpdateLabels() { foreach (Band b in bands) b.ValueLabel.Text = Fmt(b.Knob.Value); }

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
    //   書式: "# state: Treble=0.0 High-Mid=0.0 ... Bass=1.0 Sub=0.0 on"
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
                    string[] p = line.Substring(8).Trim().Split(' ');
                    if (p.Length == 0) continue;
                    foreach (string kv in p)
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 0) continue;
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
            state.Append(" " + b.Name + "=" + Fmt(b.Knob.Value));
            boost = Math.Max(boost, b.Knob.Value);
        }
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
