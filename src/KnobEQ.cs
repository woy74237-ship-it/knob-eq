// KnobEQ - Equalizer APO 用のつまみ式イコライザ (Sub / Bass / Low-Mid / Mid / High-Mid / Treble)
// つまみを回すと Equalizer APO の config フォルダ内の knobeq.txt を書き換え、即座に反映される。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

// exe のプロパティに出る情報。配布用 ZIP の名前（package.ps1）も AssemblyInformationalVersion から決まる
[assembly: AssemblyTitle("KnobEQ")]
[assembly: AssemblyProduct("KnobEQ")]
[assembly: AssemblyDescription("Equalizer APO 用のつまみ式イコライザ")]
[assembly: AssemblyCopyright("© 2026 Walt-Owen")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

// Windows のライト／ダークモードとアクセントカラーに合わせた配色
static class Theme
{
    public static bool Dark;
    public static Color Bg, Panel, Grid, Label, Value, Ring, Accent, OnAccent, WarnBg, WarnText;

    public static void Load()
    {
        Dark = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0;
#if UI_TEST
        // 見た目確認用のビルドだけ、環境変数でライト／ダークを強制できる
        string force = Environment.GetEnvironmentVariable("KNOBEQ_THEME");
        if (force == "dark") Dark = true; else if (force == "light") Dark = false;
#endif
        if (Dark)
        {
            Bg = Rgb(0x20, 0x20, 0x20); Panel = Rgb(0x2B, 0x2B, 0x2B); Grid = Rgb(0x3D, 0x3D, 0x3D);
            Label = Rgb(0xC5, 0xC5, 0xC5); Value = Rgb(0xFF, 0xFF, 0xFF); Ring = Rgb(0x9C, 0x9C, 0x9C);
            OnAccent = Rgb(0, 0, 0);
            WarnBg = Rgb(0x43, 0x35, 0x19); WarnText = Rgb(0xFF, 0xFF, 0xFF); // Windows 11 の注意バーの色
        }
        else
        {
            Bg = Rgb(0xF3, 0xF3, 0xF3); Panel = Rgb(0xFF, 0xFF, 0xFF); Grid = Rgb(0xE5, 0xE5, 0xE5);
            Label = Rgb(0x5D, 0x5D, 0x5D); Value = Rgb(0x1A, 0x1A, 0x1A); Ring = Rgb(0x86, 0x86, 0x86);
            OnAccent = Rgb(0xFF, 0xFF, 0xFF);
            WarnBg = Rgb(0xFF, 0xF4, 0xCE); WarnText = Rgb(0x1A, 0x1A, 0x1A);
        }
        // アクセントカラーは、ダークでは明るめ、ライトでは暗めの色を使う（Windows 11 と同じ使い分け）
        Color defaultAccent = Dark ? Rgb(0x4C, 0xC2, 0xFF) : Rgb(0x00, 0x5F, 0xB8);
        Accent = ReadAccent(Dark ? 1 : 4, defaultAccent);
#if UI_TEST
        // 見た目確認用のビルドだけ、Windows の既定の青に固定できる（紹介用の画面写真を撮るとき用）
        if (Environment.GetEnvironmentVariable("KNOBEQ_ACCENT") == "default") Accent = defaultAccent;
#endif
    }

    static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

    static int ReadDword(string path, string name, int fallback)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(path))
            {
                object v = k == null ? null : k.GetValue(name);
                if (v is int) return (int)v;
            }
        }
        catch { }
        return fallback;
    }

    // AccentPalette は RGBA が 8 色並んだ値（0: 最も明るい … 3: 基本色 … 6: 最も暗い）
    static Color ReadAccent(int index, Color fallback)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
            {
                byte[] p = k == null ? null : k.GetValue("AccentPalette") as byte[];
                if (p != null && p.Length >= 32) return Rgb(p[index * 4], p[index * 4 + 1], p[index * 4 + 2]);
            }
        }
        catch { }
        return fallback;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // タイトルバーもダーク／ライトに合わせる（Windows 10 20H1 以降）
    public static void ApplyTitleBar(IntPtr hwnd)
    {
        int on = Dark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, 20, ref on, 4); } catch { }
    }
}

// 今の出力先で Equalizer APO が働ける状態かを調べる
static class AudioCheck
{
    public enum Status { Ok, NoApo, EnhancementsOff, Unknown }

    const string ApoClsid = "{EACD2258-FCAC-4FF4-B36D-419E924A6D79}";
    // PKEY_AudioEndpoint_Disable_SysFx（1 なら「オーディオの機能強化」がオフ）
    const string DisableSysFx = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5";
    const string RenderKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\";

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(); // 使わない（並び順を合わせるためだけに宣言）
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    interface IMMDevice
    {
        int Activate();         // 使わない
        int OpenPropertyStore(); // 使わない
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    // 既定の出力デバイスの ID（例: {0.0.0.00000000}.{guid}）。無ければ null
    static string DefaultRenderId()
    {
        object e = null; IMMDevice dev = null;
        try
        {
            e = new MMDeviceEnumerator();
            if (((IMMDeviceEnumerator)e).GetDefaultAudioEndpoint(0 /* eRender */, 0 /* eConsole */, out dev) != 0 || dev == null) return null;
            string id;
            return dev.GetId(out id) == 0 ? id : null;
        }
        catch { return null; }
        finally
        {
            if (dev != null) Marshal.ReleaseComObject(dev);
            if (e != null) Marshal.ReleaseComObject(e);
        }
    }

    public static Status Check(out string deviceName)
    {
        deviceName = null;
#if UI_TEST
        // 見た目確認用のビルドだけ、環境変数で注意バーの状態を強制できる
        string force = Environment.GetEnvironmentVariable("KNOBEQ_WARN");
        if (force == "off") { deviceName = "Headphones"; return Status.EnhancementsOff; }
        if (force == "noapo") { deviceName = "Speakers"; return Status.NoApo; }
#endif
        string id = DefaultRenderId();
        if (id == null) return Status.Unknown;
        string guid = id.Substring(id.LastIndexOf('.') + 1);
        try
        {
            using (RegistryKey props = Registry.LocalMachine.OpenSubKey(RenderKey + guid + @"\Properties"))
                if (props != null)
                    deviceName = (props.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string)
                              ?? (props.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string);

            using (RegistryKey fx = Registry.LocalMachine.OpenSubKey(RenderKey + guid + @"\FxProperties"))
            {
                if (fx == null) return Status.NoApo;
                bool hasApo = false;
                foreach (string name in fx.GetValueNames())
                {
                    object v = fx.GetValue(name);
                    string s = v is string[] ? string.Join(",", (string[])v) : Convert.ToString(v);
                    if (s != null && s.IndexOf(ApoClsid, StringComparison.OrdinalIgnoreCase) >= 0) { hasApo = true; break; }
                }
                if (!hasApo) return Status.NoApo;
                object off = fx.GetValue(DisableSysFx);
                if (off is int && (int)off != 0) return Status.EnhancementsOff;
                return Status.Ok;
            }
        }
        catch { return Status.Unknown; }
    }
}

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

    // チューニングガイドが示す目安の範囲 (dB)
    public bool HasHint;
    public float HintLo, HintHi;
    public void SetHint(bool on, float lo, float hi)
    {
        HasHint = on; HintLo = lo; HintHi = hi;
        Invalidate();
    }

    // 値に対応するつまみの角度（真上が 0 度、時計回り。-135〜+135 度）
    static float AngleOf(float v) { return -135f + 270f * (v - Min) / (Max - Min); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float size = Math.Min(Width, Height) - 6;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        using (Pen p = new Pen(Theme.Ring, 3f))
            g.DrawEllipse(p, cx - r, cy - r, size, size);

        // チューニングガイドの目安: 輪の上に、目安の範囲をアクセントカラーの太い弧で描く
        if (HasHint)
        {
            float a0 = AngleOf(HintLo) - 90, a1 = AngleOf(HintHi) - 90; // DrawArc は 3 時の方向が 0 度
            float sweep = Math.Max(a1 - a0, 10f);
            if (a1 - a0 < 10f) a0 -= (10f - (a1 - a0)) / 2; // 範囲が 1 点のときも見えるよう 10 度は取る
            using (Pen hp = new Pen(Theme.Accent, 7f))
            {
                hp.StartCap = hp.EndCap = LineCap.Round;
                g.DrawArc(hp, cx - r, cy - r, size, size, a0, sweep);
            }
        }

        double t = (value - Min) / (Max - Min);
        double rad = (-135 + 270 * t) * Math.PI / 180.0;
        float rr = r * 0.70f, dot = Math.Max(8f, size * 0.13f);
        float x = cx + (float)Math.Sin(rad) * rr, y = cy - (float)Math.Cos(rad) * rr;
        using (Brush b = new SolidBrush(Theme.Accent))
            g.FillEllipse(b, x - dot / 2, y - dot / 2, dot, dot);
    }
}

// チューニングガイドの 1 項目（「曇りを取りたい」など）
class Recipe
{
    public string Title, Why, Tip6, Tip3;
    public Adj[] Six, Three; // 6 ノブ / 3 ノブモードでの目安
    public Adj[] For(bool three) { return three ? Three : Six; }
    public string TipFor(bool three) { return three ? Tip3 : Tip6; }
}

// 1 つのつまみの目安（dB の範囲。当てはめるときは真ん中の値を使う）
class Adj
{
    public string Band; public float Lo, Hi;
    public Adj(string band, float lo, float hi) { Band = band; Lo = lo; Hi = hi; }
    public float Mid { get { return (Lo + Hi) / 2; } }
}

static class Recipes
{
    static Adj A(string band, float lo, float hi) { return new Adj(band, lo, hi); }
    static Adj A(string band, float v) { return new Adj(band, v, v); }

    public static readonly Recipe[] All = new Recipe[] {
        new Recipe {
            Title = "曇りを取りたい",
            Why = "こもって聞こえる原因の多くは 200〜500 Hz の出すぎです。ここを少し削ると、ベールが 1 枚はがれたように聞こえます。",
            Six = new[] { A("Low-Mid", -4, -2), A("High-Mid", 1) },
            Three = new[] { A("Bass", -1), A("Treble", 2) },
            Tip6 = "まず Low-Mid だけを −2 から。削りすぎると今度は痩せた音になるので、こもりが消えたところで止めます。",
            Tip3 = "Treble を少しずつ上げ、こもりが消えたところで止めます。Bass を下げすぎると痩せた音になります。" },
        new Recipe {
            Title = "華やかにしたい",
            Why = "キラキラ感は 5 kHz より上にあります。ここを持ち上げると、シンバルや弦の響きが明るくなります。",
            Six = new[] { A("Treble", 2, 4), A("High-Mid", 1) },
            Three = new[] { A("Treble", 3), A("Mid", 1) },
            Tip6 = "上げすぎると「サ行」が耳に刺さります。歌ものを聴きながら、刺さる手前で止めます。",
            Tip3 = "上げすぎると「サ行」が耳に刺さります。歌ものを聴きながら、刺さる手前で止めます。" },
        new Recipe {
            Title = "空気感を感じたい",
            Why = "音の消え際やホールの響きは、10 kHz 以上のとても小さな成分です。高域をなだらかに持ち上げ、そのすぐ下を少し控えると、刺さらずに広がりが出ます。",
            Six = new[] { A("Treble", 2, 3), A("High-Mid", -1) },
            Three = new[] { A("Treble", 3), A("Mid", -1) },
            Tip6 = "静かな曲やライブ録音で試すと分かりやすいです。耳が疲れたら Treble を 1 戻します。",
            Tip3 = "静かな曲やライブ録音で試すと分かりやすいです。耳が疲れたら Treble を 1 戻します。" },
        new Recipe {
            Title = "重低音の振動を感じたい",
            Why = "体に響く「ズン」は 30〜60 Hz です。Sub を上げ、こもりの原因になる少し上の帯域は控えめにします。",
            Six = new[] { A("Sub", 4, 6), A("Bass", 1, 2), A("Low-Mid", -1) },
            Three = new[] { A("Bass", 4, 6) },
            Tip6 = "ヘッドホンによっては 40 Hz がほとんど出ません。上げても変わらなければ、それ以上は上げないでください（音割れ防止のため全体の音量が下がるだけです）。",
            Tip3 = "3 ノブの Bass はなだらかに効くので、最も低い帯域は 6 ノブの Sub ほど持ち上がりません。もっと欲しいときは 6 ノブモードを試してください。" },
        new Recipe {
            Title = "ボーカルを前に出したい",
            Why = "声の聞き取りやすさは 1〜4 kHz で決まります。ここを少し上げ、声を覆う低い中域を控えます。",
            Six = new[] { A("High-Mid", 2), A("Mid", 1), A("Low-Mid", -1) },
            Three = new[] { A("Mid", 2) },
            Tip6 = "上げるのは +2 まで。それ以上は耳障りになりやすいです。",
            Tip3 = "上げるのは +2 まで。それ以上は耳障りになりやすいです。" },
        new Recipe {
            Title = "刺さる音をやわらげたい",
            Why = "耳に刺さる・シャリシャリするのは、3〜8 kHz の出すぎです。ここを少し削ります。",
            Six = new[] { A("High-Mid", -3, -2), A("Treble", -2, -1) },
            Three = new[] { A("Treble", -2), A("Mid", -1) },
            Tip6 = "削りすぎるとこもった音になります。長時間聴いて疲れないところが目安です。",
            Tip3 = "削りすぎるとこもった音になります。長時間聴いて疲れないところが目安です。" },
        new Recipe {
            Title = "音に厚みがほしい",
            Why = "音の太さや温かみは 100〜400 Hz にあります。ここを少し足すと、痩せた音に厚みが出ます。",
            Six = new[] { A("Low-Mid", 2), A("Bass", 1) },
            Three = new[] { A("Bass", 2), A("Mid", 1) },
            Tip6 = "足しすぎると「曇り」になります。曇りを感じたら Low-Mid を戻します。",
            Tip3 = "足しすぎると「曇り」になります。曇りを感じたら Bass を戻します。" },
        new Recipe {
            Title = "小さい音量でも迫力がほしい",
            Why = "人の耳は、音量が小さいと低音と高音を感じにくくなります。両端を少し足すと、小音量でもバランスよく聞こえます。",
            Six = new[] { A("Bass", 3), A("Treble", 2) },
            Three = new[] { A("Bass", 3), A("Treble", 2) },
            Tip6 = "大きな音量に戻したら、つまみも戻すのがおすすめです。",
            Tip3 = "大きな音量に戻したら、つまみも戻すのがおすすめです。" },
    };
}

// 1 つのつまみが担当するフィルター
class Band
{
    public string Name, Filter; // Filter は Equalizer APO の書式 ("LS", "PK", "HS")
    public int Freq; public float Q;
    public bool InThree;  // 3 ノブモードでも表示するか
    public float ThreeQ;  // 3 ノブモードで使う Q（0 なら Q と同じ）
    public float ActiveQ; // 今のモードで使う Q。シェルフで 0 なら Equalizer APO の標準の傾き
    public float Value6, Value3; // モードごとに覚えておく値 (dB)。つまみには今のモードの値を表示する
    public Knob Knob; public Label NameLabel, ValueLabel;
    // 画面に出す名前（例: "Low-Mid  300"、"Treble  8k"）
    public string DisplayName { get { return Name + "  " + (Freq >= 1000 ? (Freq / 1000) + "k" : Freq.ToString()); } }
    public Band(string name, string filter, int freq, float q, bool inThree, float threeQ = 0f)
    { Name = name; Filter = filter; Freq = freq; Q = q; InThree = inThree; ThreeQ = threeQ; ActiveQ = q; }
}

// 全バンドを合わせた周波数特性（どれだけ持ち上げ／下げているか）を描くグラフ
class ResponseGraph : Control
{
    const double Fs = 48000;            // 計算に使うサンプリング周波数
    const double FMin = 20, FMax = 20000;
    public Band[] Bands;
    public Func<Band, bool> IsActive;
    public bool On = true;
    public Func<float> Preamp; // 自動で下げている全体の音量 (dB)。右上に表示する
    // チューニングガイドの目安。つまみごとの目安の値を返す（目安が無いつまみは今の値のまま）。null なら描かない
    public Func<Band, float> HintValue;

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
            double al = s / (2 * b.ActiveQ);
            b0 = 1 + al * A; b1 = -2 * c; b2 = 1 - al * A;
            a0 = 1 + al / A; a1 = -2 * c; a2 = 1 - al / A;
        }
        else
        {
            // Q の指定が無い LS / HS は Q 0.707 相当の傾きとして描く
            double q = b.ActiveQ > 0 ? b.ActiveQ : Math.Sqrt(0.5);
            double al = s / (2 * q), sq = 2 * Math.Sqrt(A) * al;
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

    // 効いているつまみをすべて合わせたゲイン (dB)
    double SumDb(double f) { return SumDb(f, null); }

    // value が null なら今のつまみの値、あれば value が返す値で合計する
    double SumDb(double f, Func<Band, float> value)
    {
        if (Bands == null) return 0;
        double sum = 0;
        foreach (Band b in Bands)
            if (b.Knob != null && (IsActive == null || IsActive(b)))
                sum += BandDb(b, value != null ? value(b) : b.Knob.Value, f);
        return sum;
    }

    double TotalDb(double f) { return On ? SumDb(f) : 0; }

    // 20 Hz〜20 kHz で、つまみを合わせて最も持ち上がっている量 (dB)。持ち上げていなければ 0
    // つまみ同士の効きが重なった部分も含めて調べる（各つまみの中心周波数は必ず調べる）
    public double PeakBoostDb()
    {
        double peak = 0;
        const int N = 400;
        for (int i = 0; i <= N; i++) peak = Math.Max(peak, SumDb(FMin * Math.Pow(FMax / FMin, i / (double)N)));
        if (Bands != null) foreach (Band b in Bands) peak = Math.Max(peak, SumDb(b.Freq));
        return peak;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Panel);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const int padL = 24, padR = 6, padT = 6, padB = 16;
        RectangleF area = new RectangleF(padL, padT, Width - padL - padR, Height - padT - padB);

        // 周波数ごとのゲインを計算し、縦軸の範囲を決める（±12 dB を基本に、超えたら 6 dB 刻みで広げる）
        int n = (int)area.Width + 1;
        double[] db = new double[n];
        double[] hint = HintValue != null ? new double[n] : null; // ガイドの目安どおりに回したときのカーブ
        double peak = 0;
        for (int i = 0; i < n; i++)
        {
            double f = FMin * Math.Pow(FMax / FMin, i / (double)(n - 1));
            db[i] = TotalDb(f);
            peak = Math.Max(peak, Math.Abs(db[i]));
            if (hint != null) { hint[i] = SumDb(f, HintValue); peak = Math.Max(peak, Math.Abs(hint[i])); }
        }
        double range = Math.Max(12, Math.Ceiling(peak / 6) * 6);

        Func<double, float> X = f => area.Left + (float)(Math.Log(f / FMin) / Math.Log(FMax / FMin)) * area.Width;
        Func<double, float> Y = d => area.Top + (float)((range - d) / (2 * range)) * area.Height;

        using (Font font = new Font("Segoe UI", 7f))
        using (Brush tb = new SolidBrush(Theme.Label))
        using (Pen gp = new Pen(Theme.Grid, 1f))
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
                using (Pen p = new Pen(d == 0 ? Theme.Ring : Theme.Grid, 1f)) g.DrawLine(p, area.Left, y, area.Right, y);
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
            using (Brush fb = new SolidBrush(Color.FromArgb(On ? 50 : 20, Theme.Accent))) g.FillPath(fb, fill);
        }
        using (Pen cp = new Pen(On ? Theme.Accent : Theme.Ring, 2f)) g.DrawLines(cp, pts);

        // ガイドの目安どおりに回したときのカーブを点線で重ねる
        if (hint != null)
        {
            PointF[] hp = new PointF[n];
            for (int i = 0; i < n; i++) hp[i] = new PointF(area.Left + i, Y(hint[i]));
            using (Pen p = new Pen(Theme.Value, 1.5f))
            {
                p.DashPattern = new float[] { 4, 3 };
                g.DrawLines(p, hp);
            }
        }

        // 自動で下げている全体の音量（プリアンプ）を右上に表示する。カーブと重なっても読めるよう下地を敷く
        if (On && Preamp != null)
            using (Font f = new Font("Segoe UI", 7.5f))
            using (Brush tb = new SolidBrush(Theme.Value))
            using (Brush bg = new SolidBrush(Color.FromArgb(200, Theme.Panel)))
            {
                string text = "Preamp " + Preamp().ToString("0.0", CultureInfo.InvariantCulture) + " dB";
                SizeF sz = g.MeasureString(text, f);
                RectangleF box = new RectangleF(area.Right - sz.Width - 2, area.Top + 1, sz.Width + 2, sz.Height);
                g.FillRectangle(bg, box);
                g.DrawString(text, f, tb, box.Left + 1, box.Top);
            }

        if (!On)
            using (Font f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Brush b = new SolidBrush(Theme.Label))
                g.DrawString("EQ OFF", f, b, area.Left + area.Width / 2, area.Top + 4, new StringFormat { Alignment = StringAlignment.Center });
    }
}

// チューニングガイド: 目的を選ぶと、動かすつまみと目安を本体の画面に示す（本体の横に開く）
class GuideForm : Form
{
    const int ListW = 170, RightX = 12 + ListW + 12, RightW = 320;
    readonly MainForm main;
    ListBox list;
    Label title, why, section, adjust, caption, tip;
    ResponseGraph mini;
    Button apply, undo, ab;

    public GuideForm(MainForm main)
    {
        this.main = main;
        Text = "チューニングガイド";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.Manual;
        Font = new Font("Segoe UI", 9f);

        Label head = NewLabel(12, 12, ListW, 10f, FontStyle.Bold);
        head.Text = "どうしたい？";
        head.Name = "head";

        list = new ListBox();
        list.Bounds = new Rectangle(12, 40, ListW, 8 * 30 + 4);
        list.BorderStyle = BorderStyle.None;
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = 30;
        list.IntegralHeight = false;
        foreach (Recipe r in Recipes.All) list.Items.Add(r);
        list.DrawItem += DrawListItem;
        list.SelectedIndexChanged += delegate { ShowRecipe(); };
        Controls.Add(list);

        title = NewLabel(RightX, 12, RightW, 12f, FontStyle.Bold);
        why = NewLabel(RightX, 0, RightW, 9f, FontStyle.Regular);
        section = NewLabel(RightX, 0, RightW, 8.5f, FontStyle.Regular);
        adjust = NewLabel(RightX, 0, RightW, 10f, FontStyle.Bold);

        mini = new ResponseGraph();
        mini.Bands = main.Bands;
        mini.IsActive = main.IsActive;
        mini.HintValue = main.HintValue;
        mini.Size = new Size(RightW, 110);
        Controls.Add(mini);
        caption = NewLabel(RightX, 0, RightW, 8f, FontStyle.Regular);
        caption.Text = "実線: 今のカーブ　点線: 目安どおりに回したときのカーブ";
        tip = NewLabel(RightX, 0, RightW, 9f, FontStyle.Regular);

        apply = NewButton("目安を当てはめる", 128);
        undo = NewButton("元に戻す", 84);
        ab = NewButton("聴き比べ", 96);
        ToolTip tips = new ToolTip();
        tips.SetToolTip(apply, "目安の真ん中の値に、つまみをまとめて動かします");
        tips.SetToolTip(undo, "「目安を当てはめる」を押す前の値に戻します");
        tips.SetToolTip(ab, "EQ の ON / OFF を切り替えて、補正のありなしを聴き比べます");
        apply.Click += delegate { Recipe r = Selected; if (r != null) main.ApplyRecipe(r); UpdateButtons(); };
        undo.Click += delegate { main.UndoRecipe(); UpdateButtons(); };
        ab.Click += delegate { main.ToggleEq(); };

        list.SelectedIndex = 0;
        ApplyTheme();
    }

    Recipe Selected { get { return list.SelectedItem as Recipe; } }

    Label NewLabel(int x, int y, int w, float size, FontStyle style)
    {
        Label l = new Label();
        l.Location = new Point(x, y);
        l.Width = w;
        l.Font = new Font("Segoe UI", size, style);
        l.BackColor = Color.Transparent;
        Controls.Add(l);
        return l;
    }

    Button NewButton(string text, int w)
    {
        Button b = new Button();
        b.Text = text;
        b.FlatStyle = FlatStyle.Flat;
        b.Size = new Size(w, 30);
        Controls.Add(b);
        return b;
    }

    // ラベルの高さを文章に合わせて決め、次に置く位置を返す
    int Place(Label l, int y, int gapAfter)
    {
        l.Top = y;
        l.Height = TextRenderer.MeasureText(l.Text, l.Font, new Size(l.Width, 0), TextFormatFlags.WordBreak).Height + 2;
        return l.Bottom + gapAfter;
    }

    static string Db(float v)
    {
        string s = v.ToString("0.#", CultureInfo.InvariantCulture);
        return v > 0 ? "+" + s : v < 0 ? "−" + s.Substring(1) : s;
    }

    // 選んだ目的の説明を、今のモード（3 / 6 ノブ）に合わせて表示し、本体のつまみとグラフに目安を示す
    public void ShowRecipe()
    {
        Recipe r = Selected;
        if (r == null) return;
        bool three = main.ThreeMode;
        title.Text = r.Title;
        why.Text = r.Why;
        section.Text = "動かすつまみ（" + (three ? "3" : "6") + " ノブ）";
        StringBuilder sb = new StringBuilder();
        foreach (Adj a in r.For(three))
        {
            Band b = main.FindBand(a.Band);
            // 「−2 〜 −4」のように、小さく動かす側から書く
            string range = a.Lo == a.Hi ? Db(a.Lo) : a.Hi <= 0 ? Db(a.Hi) + " 〜 " + Db(a.Lo) : Db(a.Lo) + " 〜 " + Db(a.Hi);
            sb.AppendLine(b.DisplayName + "　" + range + " dB");
        }
        adjust.Text = sb.ToString().TrimEnd();
        tip.Text = "コツ: " + r.TipFor(three);

        SuspendLayout();
        int y = Place(title, 12, 6);
        y = Place(why, y, 10);
        y = Place(section, y, 2);
        y = Place(adjust, y, 10);
        mini.Location = new Point(RightX, y);
        y = Place(caption, mini.Bottom + 3, 10);
        y = Place(tip, y, 14);
        apply.Location = new Point(RightX, y);
        undo.Location = new Point(apply.Right + 6, y);
        ab.Location = new Point(undo.Right + 6, y);
        ClientSize = new Size(RightX + RightW + 12, Math.Max(ab.Bottom, list.Bottom) + 14);
        ResumeLayout();

        main.ShowHint(r);
        mini.Invalidate();
        UpdateButtons();
        FollowMain();
    }

    public void UpdateButtons() { undo.Enabled = main.CanUndoRecipe; mini.Invalidate(); }

    // 本体の右隣に置く。右に入りきらなければ左隣に置く
    public void FollowMain()
    {
        Rectangle area = Screen.FromControl(main).WorkingArea;
        int x = main.Right + 6;
        if (x + Width > area.Right) x = Math.Max(area.Left, main.Left - 6 - Width);
        int y = Math.Max(area.Top, Math.Min(main.Top, area.Bottom - Height));
        Location = new Point(x, y);
    }

    void DrawListItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (Brush bg = new SolidBrush(sel ? Theme.Panel : Theme.Bg)) e.Graphics.FillRectangle(bg, e.Bounds);
        if (sel) using (Brush bar = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top + 4, 3, e.Bounds.Height - 8);
        TextRenderer.DrawText(e.Graphics, ((Recipe)list.Items[e.Index]).Title, list.Font,
            new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 10, e.Bounds.Height),
            sel ? Theme.Value : Theme.Label, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Bg;
        list.BackColor = Theme.Bg;
        foreach (Control c in Controls)
        {
            Label l = c as Label;
            if (l != null) l.ForeColor = Theme.Label;
        }
        title.ForeColor = Theme.Value;
        Controls["head"].ForeColor = Theme.Value;
        adjust.ForeColor = Theme.Accent;
        foreach (Button b in new[] { apply, undo, ab })
        {
            bool primary = b == apply;
            b.BackColor = primary ? Theme.Accent : Theme.Bg;
            b.ForeColor = primary ? Theme.OnAccent : Theme.Label;
            b.FlatAppearance.BorderColor = primary ? Theme.Accent : Theme.Ring;
            b.FlatAppearance.MouseOverBackColor = primary ? Theme.Accent : Theme.Panel;
        }
        if (IsHandleCreated) Theme.ApplyTitleBar(Handle);
        list.Invalidate();
        mini.Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        main.ShowHint(null);
        base.OnFormClosed(e);
    }
}

class MainForm : Form
{
    // 画面の上から順に並ぶ（高音 → 低音）
    // 3 ノブモードでは 3 つで全帯域を受け持つので、シェルフの傾きをゆるめ、Mid を広めにする
    Band[] bands = new Band[] {
        new Band("Treble",   "HS", 8000, 0f,   true, 0.5f),
        new Band("High-Mid", "PK", 3000, 1.0f, false),
        new Band("Mid",      "PK", 1000, 1.0f, true, 0.7f),
        new Band("Low-Mid",  "PK", 300,  1.0f, false),
        new Band("Bass",     "LS", 100,  0f,   true, 0.5f),
        new Band("Sub",      "PK", 40,   1.0f, false),
    };

    const int ContentTop = 45, GraphHeight = 130;
    const int Step = 105, KnobSize = 80;
    const int Edge = 12, CellWidth = 216, Columns = 3; // 1 つのつまみ（つまみ + 名前と値）の幅。横レイアウトは 3 列

    bool horizontal; // true: 横レイアウト（グラフの下につまみを 3 つずつ並べる）、false: 縦に 1 列
    Button layoutBtn;
    ToolTip tip;

    Label titleLabel;
    Label warnLabel;   // EQ が効かない状態のときに出す注意（グラフの上）
    AudioCheck.Status warnStatus = AudioCheck.Status.Ok;
    bool warnShown;
    string warnDevice;
    Timer checkTimer = new Timer();
    CheckBox onBox;
    Button reset, mode3Btn, mode6Btn;
    ResponseGraph graph;
    bool threeMode; // true: 3 ノブ (Treble / Mid / Bass)、false: 6 ノブ
    Timer saveTimer = new Timer();
    string configDir, eqFile;
    bool loading;

    public MainForm()
    {
        Theme.Load();
        Text = "Knob EQ";
        // タイトルバーのアイコン（build.ps1 で埋め込んだ KnobEQ.ico。無ければ Windows 標準のまま）
        using (Stream s = typeof(MainForm).Assembly.GetManifestResourceStream("KnobEQ.ico"))
            if (s != null) Icon = new Icon(s);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        titleLabel = AddLabel("EQUALIZER", 18, 10, 110, 11f, FontStyle.Bold, ContentAlignment.MiddleLeft);

        // 右上のボタン: 縦／横レイアウトの切り替えと、3 / 6 ノブの切り替え（位置は DoLayout で決める）
        tip = new ToolTip();
        layoutBtn = AddButton("", 0, 0, 28, 24);
        layoutBtn.Click += delegate { SetHorizontal(!horizontal); };
        mode3Btn = AddButton("3", 0, 0, 28, 24);
        mode6Btn = AddButton("6", 0, 0, 28, 24);
        tip.SetToolTip(mode3Btn, "3 ノブ（Treble / Mid / Bass）");
        tip.SetToolTip(mode6Btn, "6 ノブ（全バンド）");
        mode3Btn.Click += delegate { SetMode(true); };
        mode6Btn.Click += delegate { SetMode(false); };

        // 注意バー（ふだんは隠しておく。クリックで直すための設定画面を開く）
        warnLabel = new Label();
        warnLabel.Visible = false;
        warnLabel.Font = new Font("Segoe UI", 8.5f);
        warnLabel.Padding = new Padding(6, 4, 6, 4);
        warnLabel.Cursor = Cursors.Hand;
        warnLabel.Click += delegate { OpenFixSettings(); };
        Controls.Add(warnLabel);

        // 補正カーブのグラフ（見出しの下。大きさは DoLayout で決める）
        graph = new ResponseGraph();
        graph.Bands = bands;
        graph.IsActive = IsActive;
        graph.Preamp = Preamp;
        Controls.Add(graph);

        foreach (Band b in bands)
        {
            b.Knob = new Knob();
            b.Knob.Size = new Size(KnobSize, KnobSize);
            Controls.Add(b.Knob);

            b.NameLabel = AddLabel(b.DisplayName, 0, 0, 105, 10f, FontStyle.Regular, ContentAlignment.MiddleCenter);
            b.ValueLabel = AddLabel("0.0", 0, 0, 105, 20f, FontStyle.Regular, ContentAlignment.MiddleCenter);

            tip.SetToolTip(b.Knob, "上下ドラッグ / ホイールで調整（Shift で微調整）\nダブルクリックで 0 dB");
            b.Knob.ValueChanged += delegate
            {
                // 表示中のつまみの値を、今のモードの値として覚える
                if (IsActive(b)) { if (threeMode) b.Value3 = b.Knob.Value; else b.Value6 = b.Knob.Value; }
                UpdateLabels();
                ScheduleSave();
            };
        }

        onBox = new CheckBox();
        onBox.Text = "EQ ON";
        onBox.Checked = true;
        onBox.AutoSize = true;
        onBox.CheckedChanged += delegate { graph.On = onBox.Checked; graph.Invalidate(); ScheduleSave(); };
        Controls.Add(onBox);

        // チューニングガイドを開く／閉じる
        hintBtn = AddButton("ヒント", 0, 0, 56, 28);
        tip.SetToolTip(hintBtn, "目的に合わせたチューニングのヒント");
        hintBtn.Click += delegate { ToggleGuide(); };
        LocationChanged += delegate { if (guide != null) guide.FollowMain(); };
        SizeChanged += delegate { if (guide != null) guide.FollowMain(); };

        reset = AddButton("Reset", 0, 0, 75, 28);
        // 今のモードのつまみをすべて 0 に戻す（もう一方のモードの値は変えない）
        reset.Click += delegate { foreach (Band b in bands) if (IsActive(b)) b.Knob.Value = 0; };

        saveTimer.Interval = 120;
        saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };

        configDir = FindConfigDir();
        eqFile = Path.Combine(configDir, "knobeq.txt");
        Load_();
        CheckAudio();
        DoLayout();
        ApplyTheme();
        EnsureInclude();
        Save();

        // 出力先の切り替えや設定の変更に気づけるよう、数秒ごとに調べ直す
        checkTimer.Interval = 3000;
        checkTimer.Tick += delegate { CheckAudio(); };
        checkTimer.Start();

        // Windows の設定でライト／ダークやアクセントカラーが変わったら、すぐに配色を切り替える
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color &&
            e.Category != UserPreferenceCategory.VisualStyle) return;
        if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { Theme.Load(); ApplyTheme(); });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnFormClosed(e);
    }

    // 今のテーマの色を各部品に塗り直す
    void ApplyTheme()
    {
        BackColor = Theme.Bg;
        titleLabel.ForeColor = Theme.Label;
        foreach (Band b in bands)
        {
            b.NameLabel.ForeColor = Theme.Label;
            b.ValueLabel.ForeColor = Theme.Value;
            b.Knob.Invalidate();
        }
        onBox.ForeColor = Theme.Label;
        warnLabel.BackColor = Theme.WarnBg;
        warnLabel.ForeColor = Theme.WarnText;
        foreach (Button btn in new Button[] { layoutBtn, mode3Btn, mode6Btn, hintBtn, reset })
        {
            btn.FlatAppearance.BorderColor = Theme.Ring;
            btn.FlatAppearance.MouseOverBackColor = Theme.Panel;
        }
        StyleModeButton(reset, false);
        StyleModeButton(layoutBtn, false);
        StyleModeButton(hintBtn, guide != null);
        if (guide != null) guide.ApplyTheme();
        if (hintRecipe != null) ShowHint(hintRecipe); // ガイドで光らせているつまみの名前を塗り直す
        StyleModeButton(mode3Btn, threeMode);
        StyleModeButton(mode6Btn, !threeMode);
        graph.Invalidate();
        if (IsHandleCreated) Theme.ApplyTitleBar(Handle);
        Invalidate(true);
    }

    // 今の出力先で EQ が効く状態かを調べ、変わっていれば注意バーを出し直す
    void CheckAudio()
    {
        string device;
        AudioCheck.Status s = AudioCheck.Check(out device);
        if (s == warnStatus && device == warnDevice) return;
        warnStatus = s; warnDevice = device;

        string name = string.IsNullOrEmpty(device) ? "今の出力先" : "「" + device + "」";
        if (s == AudioCheck.Status.EnhancementsOff)
            warnLabel.Text = "⚠ " + name + "の「オーディオの機能強化」がオフのため、EQ が効いていません。\nクリックでサウンド設定を開く";
        else if (s == AudioCheck.Status.NoApo)
            warnLabel.Text = "⚠ " + name + "には Equalizer APO が入っていないため、EQ が効きません。\nクリックで Equalizer APO の設定を開く";
        // ウィンドウ表示前は Visible が常に false を返すので、表示するかどうかは自前で覚えておく
        warnShown = s == AudioCheck.Status.EnhancementsOff || s == AudioCheck.Status.NoApo;
        warnLabel.Visible = warnShown;
        if (IsHandleCreated) DoLayout(); // 起動時はこのあとコンストラクターで並べる
    }

    void OpenFixSettings()
    {
        try
        {
            if (warnStatus == AudioCheck.Status.NoApo)
                System.Diagnostics.Process.Start(Path.Combine(Path.GetDirectoryName(configDir), "DeviceSelector.exe"));
            else
                System.Diagnostics.Process.Start("ms-settings:sound");
        }
        catch (Exception ex) { MessageBox.Show("設定画面を開けませんでした:\n" + ex.Message, Text); }
    }

    public bool IsActive(Band b) { return !threeMode || b.InThree; }

    // ---- チューニングガイドとのやりとり ----
    GuideForm guide;
    Button hintBtn;
    Recipe hintRecipe;      // ガイドで選んでいる目的（ガイドを閉じると null）
    float[] undoValues;     // 「目安を当てはめる」前のつまみの値（「元に戻す」用）
    bool undoThree;         // 上の値を覚えたときのモード

    public Band[] Bands { get { return bands; } }
    public bool ThreeMode { get { return threeMode; } }
    public Band FindBand(string name) { return Find(name); }
    public bool CanUndoRecipe { get { return undoValues != null && undoThree == threeMode; } }

    void ToggleGuide()
    {
        if (guide != null) { guide.Close(); return; }
        guide = new GuideForm(this);
        guide.FormClosed += delegate { guide = null; StyleModeButton(hintBtn, false); };
        guide.Show(this); // 本体に従うウィンドウとして開く（本体を閉じると一緒に閉じる）
        guide.FollowMain();
        StyleModeButton(hintBtn, true);
    }

    // 今のモードでのその目的の目安。目安が無いつまみは null
    Adj HintFor(Band b)
    {
        if (hintRecipe == null || !IsActive(b)) return null;
        foreach (Adj a in hintRecipe.For(threeMode)) if (a.Band == b.Name) return a;
        return null;
    }

    // グラフの点線用: 目安どおりに回したときの値（目安が無いつまみは今の値）
    public float HintValue(Band b)
    {
        Adj a = HintFor(b);
        return a != null ? a.Mid : b.Knob.Value;
    }

    // 目安のあるつまみを光らせ、グラフに目安のカーブを重ねる。null で消す
    public void ShowHint(Recipe r)
    {
        hintRecipe = r;
        foreach (Band b in bands)
        {
            Adj a = HintFor(b);
            b.Knob.SetHint(a != null, a != null ? a.Lo : 0, a != null ? a.Hi : 0);
            // アクセントカラーがグレーに近い設定でも分かるよう、色に加えて太字にする
            b.NameLabel.ForeColor = a != null ? Theme.Accent : Theme.Label;
            b.NameLabel.Font = new Font(b.NameLabel.Font, a != null ? FontStyle.Bold : FontStyle.Regular);
        }
        graph.HintValue = r != null ? (Func<Band, float>)HintValue : null;
        graph.Invalidate();
    }

    // 目安の真ん中の値につまみを動かす。最初の 1 回だけ、動かす前の値を覚えておく
    public void ApplyRecipe(Recipe r)
    {
        if (!CanUndoRecipe)
        {
            undoValues = new float[bands.Length];
            for (int i = 0; i < bands.Length; i++) undoValues[i] = bands[i].Knob.Value;
            undoThree = threeMode;
        }
        foreach (Band b in bands)
        {
            if (!IsActive(b)) continue;
            foreach (Adj a in r.For(threeMode)) if (a.Band == b.Name) b.Knob.Value = a.Mid;
        }
    }

    public void UndoRecipe()
    {
        if (!CanUndoRecipe) return;
        for (int i = 0; i < bands.Length; i++) if (IsActive(bands[i])) bands[i].Knob.Value = undoValues[i];
        undoValues = null;
    }

    public void ToggleEq() { onBox.Checked = !onBox.Checked; }

    void SetMode(bool three)
    {
        if (three == threeMode) return;
        threeMode = three;
        ShowModeValues();
        DoLayout();
        if (guide != null) guide.ShowRecipe(); // ガイドの目安も今のモード用に出し直す
        ScheduleSave();
    }

    // つまみに今のモードの値を表示する
    void ShowModeValues()
    {
        bool wasLoading = loading;
        loading = true; // 途中で保存が走らないようにする
        foreach (Band b in bands) b.Knob.Value = threeMode && b.InThree ? b.Value3 : b.Value6;
        loading = wasLoading;
        UpdateLabels();
    }

    void SetHorizontal(bool h)
    {
        if (h == horizontal) return;
        horizontal = h;
        DoLayout();
        ScheduleSave();
    }

    // 部品を並べ、ウィンドウの大きさを合わせる
    //   縦: グラフの下につまみを 1 列に並べる（上から高音 → 低音）
    //   横: グラフの下につまみを 3 つずつ並べる（左上から低音 → 高音。グラフの横軸と同じ向き）
    void DoLayout()
    {
        SuspendLayout();
        int cols = horizontal ? Columns : 1;
        int contentW = cols * CellWidth;
        int width = contentW + Edge * 2;

        // 見出しの右側のボタン（右端にそろえる）
        mode6Btn.Location = new Point(width - 44, 12);
        mode3Btn.Location = new Point(width - 74, 12);
        layoutBtn.Location = new Point(width - 110, 12);
        layoutBtn.Text = horizontal ? "↕" : "↔";
        tip.SetToolTip(layoutBtn, horizontal ? "縦レイアウトにする" : "横レイアウトにする");

        int top = ContentTop;
        if (warnShown)
        {
            Size need = TextRenderer.MeasureText(warnLabel.Text, warnLabel.Font, new Size(contentW - 12, 0), TextFormatFlags.WordBreak);
            warnLabel.Bounds = new Rectangle(Edge, top, contentW, need.Height + 8 + 4);
            top = warnLabel.Bottom + 8;
        }
        graph.Bounds = new Rectangle(Edge, top, contentW, GraphHeight);
        int knobTop = graph.Bottom + 15;

        int index = 0;
        for (int i = 0; i < bands.Length; i++)
        {
            Band b = bands[horizontal ? bands.Length - 1 - i : i];
            b.ActiveQ = threeMode && b.ThreeQ > 0 ? b.ThreeQ : b.Q;
            bool show = IsActive(b);
            b.Knob.Visible = b.NameLabel.Visible = b.ValueLabel.Visible = show;
            if (!show) continue;
            int x = Edge + (index % cols) * CellWidth, y = knobTop + (index / cols) * Step;
            b.Knob.Location = new Point(x + 13, y);            // つまみは左
            b.NameLabel.Location = new Point(x + 108, y + 2);  // 名前と値は右
            b.ValueLabel.Location = new Point(x + 108, y + 28);
            index++;
        }
        int rows = (index + cols - 1) / cols;
        int bottom = knobTop + rows * Step;
        onBox.Location = new Point(20, bottom + 12);
        reset.Location = new Point(width - 95, bottom + 7);
        hintBtn.Location = new Point(reset.Left - hintBtn.Width - 6, bottom + 7);
        ClientSize = new Size(width, bottom + 50);
        KeepOnScreen();

        // 選択中のモードのボタンを塗りつぶして示す
        StyleModeButton(mode3Btn, threeMode);
        StyleModeButton(mode6Btn, !threeMode);
        graph.Invalidate();
        ResumeLayout();
    }

    // 大きさを変えた結果、画面からはみ出したら内側に戻す
    void KeepOnScreen()
    {
        if (!IsHandleCreated) return;
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int x = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
        int y = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
        if (x != Left || y != Top) Location = new Point(x, y);
    }

    void StyleModeButton(Button btn, bool selected)
    {
        btn.BackColor = selected ? Theme.Accent : Theme.Bg;
        btn.ForeColor = selected ? Theme.OnAccent : Theme.Label;
        btn.FlatAppearance.BorderColor = selected ? Theme.Accent : Theme.Ring;
        btn.FlatAppearance.MouseOverBackColor = selected ? Theme.Accent : Theme.Panel;
    }

    Button AddButton(string text, int x, int y, int w, int h)
    {
        Button btn = new Button();
        btn.Text = text;
        btn.FlatStyle = FlatStyle.Flat;
        btn.Location = new Point(x, y);
        btn.Size = new Size(w, h);
        Controls.Add(btn);
        return btn;
    }

    Label AddLabel(string text, int x, int y, int w, float size, FontStyle style, ContentAlignment align)
    {
        Label l = new Label();
        l.Text = text;
        l.Location = new Point(x, y);
        l.Size = new Size(w, (int)(size * 1.9f));
        l.Font = new Font(size >= 20 ? "Segoe UI Light" : "Segoe UI", size, style);
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
        if (guide != null) guide.UpdateButtons();
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
    //   書式: "# state: Treble=0.0 ... Sub=0.0 3:Treble=0.0 3:Mid=0.0 3:Bass=0.0 mode=6 layout=v on"
    //   "3:" が付いたものが 3 ノブモードの値、付いていないものが 6 ノブモードの値
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
                    bool hasThree = false;
                    string[] p = line.Substring(8).Trim().Split(' ');
                    if (p.Length == 0) continue;
                    foreach (string kv in p)
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 0) continue;
                        if (kv.StartsWith("mode=")) { threeMode = kv == "mode=3"; continue; }
                        if (kv.StartsWith("layout=")) { horizontal = kv == "layout=h"; continue; }
                        bool three = kv.StartsWith("3:");
                        string name = kv.Substring(three ? 2 : 0, eq - (three ? 2 : 0));
                        Band b = Find(name);
                        if (b == null) continue;
                        float v = Math.Max(Knob.Min, Math.Min(Knob.Max, Parse(kv.Substring(eq + 1))));
                        if (three) { b.Value3 = v; hasThree = true; } else b.Value6 = v;
                    }
                    // 値が 1 組しか無い古い形式では、3 ノブにも同じ値を使う
                    if (!hasThree) foreach (Band b in bands) b.Value3 = b.Value6;
                    onBox.Checked = p[p.Length - 1] != "off";
                }
            }
        }
        catch { }
        ShowModeValues();
        loading = false;
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

    // ブーストした分だけ全体を下げて音割れ（クリップ）を防ぐ。
    // つまみ同士の重なりも含めた補正カーブの最大値を、0.1 dB 単位で切り上げて下げる
    float Preamp()
    {
        double peak = graph.PeakBoostDb();
        return -(float)(Math.Ceiling(peak * 10 - 1e-6) / 10) + 0f; // + 0f で "-0.0" 表示を避ける
    }

    void Save()
    {
        bool on = onBox.Checked;
        StringBuilder state = new StringBuilder("# state:");
        foreach (Band b in bands) state.Append(" " + b.Name + "=" + Fmt(b.Value6));
        foreach (Band b in bands)
            if (b.InThree) state.Append(" 3:" + b.Name + "=" + Fmt(b.Value3));
        state.Append(threeMode ? " mode=3" : " mode=6");
        state.Append(horizontal ? " layout=h" : " layout=v");
        state.Append(on ? " on" : " off");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# KnobEQ が自動生成するファイルです（手で編集しても上書きされます）");
        sb.AppendLine(state.ToString());
        if (on)
        {
            sb.AppendLine("Device: all");
            sb.AppendLine("Channel: all");
            sb.AppendLine("Preamp: " + Fmt(Preamp()) + " dB");
            // 低音から順に書く
            for (int i = bands.Length - 1; i >= 0; i--)
            {
                Band b = bands[i];
                if (!IsActive(b)) continue; // 3 ノブモードでは隠れているつまみを効かせない
                // Q を指定するシェルフは LSC / HSC と書く
                string type = b.Filter != "PK" && b.ActiveQ > 0 ? b.Filter + "C" : b.Filter;
                string line = "Filter: ON " + type + " Fc " + b.Freq + " Hz Gain " + Fmt(b.Knob.Value) + " dB";
                if (b.ActiveQ > 0) line += " Q " + b.ActiveQ.ToString("0.0##", CultureInfo.InvariantCulture);
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
