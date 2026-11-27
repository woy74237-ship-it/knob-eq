// アニメーション GIF を書き出す小さな部品（tools/make-demo-gif.ps1 から使う）
// ・全コマの色を数えて、よく使われる 255 色をパレットにする（UI の画面写真向け）
// ・前のコマと同じ色の画素は透明にして、変わったところだけを書く（ファイルを小さくするため）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class GifWriter
{
    const int Transparent = 255;

    // 単体の道具として使う: GifWriter.exe <出力.gif> <コマの一覧.txt>
    //   一覧は 1 行に「画像ファイルのパス<TAB>表示時間（1/100 秒）」
    public static int Main(string[] args)
    {
        var frames = new List<string>(); var delays = new List<int>();
        foreach (string line in File.ReadAllLines(args[1]))
        {
            if (line.Trim().Length == 0) continue;
            string[] p = line.Split('\t');
            frames.Add(p[0]); delays.Add(int.Parse(p[1]));
        }
        Write(args[0], frames.ToArray(), delays.ToArray());
        return 0;
    }

    // frames: コマの画像ファイル（すべて同じ大きさ）、delays: 各コマの表示時間（1/100 秒）
    public static void Write(string path, string[] frames, int[] delays)
    {
        var pixels = new List<int[]>();
        int w = 0, h = 0;
        foreach (string f in frames)
        {
            using (var bmp = new Bitmap(f)) { w = bmp.Width; h = bmp.Height; pixels.Add(Read(bmp)); }
        }

        // パレット: 15 bit（各 5 bit）に丸めた色の出現数を数え、多い順に 255 色
        var count = new int[32768];
        foreach (int[] px in pixels) foreach (int c in px) count[Key(c)]++;
        var keys = new List<int>();
        for (int k = 0; k < 32768; k++) if (count[k] > 0) keys.Add(k);
        keys.Sort((a, b) => count[b].CompareTo(count[a]));
        int n = Math.Min(255, keys.Count);
        var pal = new int[256];
        for (int i = 0; i < n; i++) pal[i] = FromKey(keys[i]);

        // 15 bit の色 → いちばん近いパレットの番号
        var map = new byte[32768];
        for (int k = 0; k < 32768; k++)
        {
            int c = FromKey(k), best = 0; long bestD = long.MaxValue;
            for (int i = 0; i < n; i++)
            {
                long d = Dist(c, pal[i]);
                if (d < bestD) { bestD = d; best = i; if (d == 0) break; }
            }
            map[k] = (byte)best;
        }

        using (var s = new FileStream(path, FileMode.Create))
        {
            WriteAscii(s, "GIF89a");
            WriteShort(s, w); WriteShort(s, h);
            s.WriteByte(0xF7); s.WriteByte(0); s.WriteByte(0); // 256 色のパレットあり
            for (int i = 0; i < 256; i++) { s.WriteByte((byte)(pal[i] >> 16)); s.WriteByte((byte)(pal[i] >> 8)); s.WriteByte((byte)pal[i]); }
            // くり返し再生
            s.WriteByte(0x21); s.WriteByte(0xFF); s.WriteByte(11); WriteAscii(s, "NETSCAPE2.0");
            s.WriteByte(3); s.WriteByte(1); WriteShort(s, 0); s.WriteByte(0);

            int[] prev = null;
            var idx = new byte[w * h];
            for (int f = 0; f < pixels.Count; f++)
            {
                int[] px = pixels[f];
                for (int i = 0; i < px.Length; i++)
                    idx[i] = (prev != null && px[i] == prev[i]) ? (byte)Transparent : map[Key(px[i])];
                // 表示時間・透明色（前のコマを残したまま重ねる）
                s.WriteByte(0x21); s.WriteByte(0xF9); s.WriteByte(4);
                s.WriteByte((byte)((1 << 2) | 1)); WriteShort(s, delays[f]); s.WriteByte(Transparent); s.WriteByte(0);
                s.WriteByte(0x2C); WriteShort(s, 0); WriteShort(s, 0); WriteShort(s, w); WriteShort(s, h); s.WriteByte(0);
                Lzw(s, idx);
                prev = px;
            }
            s.WriteByte(0x3B);
        }
    }

    static int[] Read(Bitmap bmp)
    {
        var r = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var d = bmp.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new int[bmp.Width * bmp.Height];
        for (int y = 0; y < bmp.Height; y++)
            Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), px, y * bmp.Width, bmp.Width);
        bmp.UnlockBits(d);
        for (int i = 0; i < px.Length; i++) px[i] &= 0xFFFFFF;
        return px;
    }

    static int Key(int c) { return ((c >> 19) & 31) << 10 | ((c >> 11) & 31) << 5 | ((c >> 3) & 31); }
    static int FromKey(int k)
    {
        int r = (k >> 10) & 31, g = (k >> 5) & 31, b = k & 31;
        return ((r << 3) | (r >> 2)) << 16 | ((g << 3) | (g >> 2)) << 8 | ((b << 3) | (b >> 2));
    }
    static long Dist(int a, int b)
    {
        int dr = ((a >> 16) & 255) - ((b >> 16) & 255), dg = ((a >> 8) & 255) - ((b >> 8) & 255), db = (a & 255) - (b & 255);
        return 3L * dr * dr + 4L * dg * dg + 2L * db * db;
    }

    // 画素の番号を LZW で圧縮して書く（最小符号長 8）
    static void Lzw(Stream s, byte[] data)
    {
        const int clear = 256, eoi = 257;
        s.WriteByte(8);
        var buf = new List<byte>();
        int bitBuf = 0, bitCount = 0, codeSize = 9, next = 258;
        var dict = new Dictionary<int, int>();
        Action<int> emit = code =>
        {
            bitBuf |= code << bitCount; bitCount += codeSize;
            while (bitCount >= 8) { buf.Add((byte)bitBuf); bitBuf >>= 8; bitCount -= 8; }
        };
        emit(clear);
        int prefix = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            int k = data[i], key = (prefix << 8) | k, code;
            if (dict.TryGetValue(key, out code)) { prefix = code; continue; }
            emit(prefix);
            if (next < 4096)
            {
                dict[key] = next++;
                if (next > (1 << codeSize) && codeSize < 12) codeSize++;
            }
            else
            {
                emit(clear); dict.Clear(); next = 258; codeSize = 9;
            }
            prefix = k;
        }
        emit(prefix);
        emit(eoi);
        if (bitCount > 0) buf.Add((byte)bitBuf);
        for (int i = 0; i < buf.Count; i += 255)
        {
            int len = Math.Min(255, buf.Count - i);
            s.WriteByte((byte)len);
            s.Write(buf.ToArray(), i, len);
        }
        s.WriteByte(0);
    }

    static void WriteShort(Stream s, int v) { s.WriteByte((byte)v); s.WriteByte((byte)(v >> 8)); }
    static void WriteAscii(Stream s, string t) { foreach (char c in t) s.WriteByte((byte)c); }
}
