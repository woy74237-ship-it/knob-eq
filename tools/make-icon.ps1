# KnobEQ のアイコン（src\KnobEQ.ico）を作るスクリプト
# 濃い紺グレーの角丸の地に、つまみの輪と青い点を描く。
# 16〜256px の各サイズを描き分け、PNG 形式で 1 つの .ico にまとめる。
#   使い方: .\tools\make-icon.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$tileColor = [Drawing.Color]::FromArgb(0x1F, 0x29, 0x33)
$ringColor = [Drawing.Color]::FromArgb(0x9F, 0xB4, 0xBD)
$dotColor  = [Drawing.Color]::FromArgb(0x4C, 0xC2, 0xFF)
$edgeColor = [Drawing.Color]::FromArgb(0x3E, 0x4C, 0x59)

function New-IconPng([int]$s) {
    # 64 x 64 の設計図を s ピクセルに縮める。小さいサイズでは線と点が細くなりすぎないよう下限を設ける
    $k = $s / 64.0
    $bmp = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([Drawing.Color]::Transparent)

    # 角丸の地
    $m = if ($s -le 24) { 0.0 } else { 2 * $k }
    $w = $s - 2 * $m; $r = 14 * $k
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($m + $w - 2 * $r, $m, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($m + $w - 2 * $r, $m + $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($m, $m + $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object Drawing.SolidBrush $tileColor), $path)
    # ダークのタスクバーやタイトルバーで地が溶け込まないよう、ふちに少し明るい線を入れる（32px 以上）
    if ($s -ge 32) {
        $edge = New-Object Drawing.Pen $edgeColor, ([Math]::Max(1.0, 1.5 * $k))
        $edge.Alignment = [Drawing.Drawing2D.PenAlignment]::Inset
        $g.DrawPath($edge, $path)
    }

    # つまみの輪
    $c = 32 * $k; $ringR = 18 * $k
    $ringW = [Math]::Max(5 * $k, 1.6)
    $pen = New-Object Drawing.Pen $ringColor, $ringW
    $g.DrawEllipse($pen, $c - $ringR, $c - $ringR, 2 * $ringR, 2 * $ringR)

    # 右上を指す点
    $dotR = [Math]::Max(5 * $k, 1.9)
    $dx = 41.5 * $k; $dy = 22.5 * $k
    $g.FillEllipse((New-Object Drawing.SolidBrush $dotColor), $dx - $dotR, $dy - $dotR, 2 * $dotR, 2 * $dotR)

    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$images = foreach ($s in $sizes) { , (New-IconPng $s) }

# .ico の書式: ヘッダー (6 バイト) + 各画像の目次 (16 バイトずつ) + 画像データ
$out = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $out
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $images[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $images) { $bw.Write($data) }
$bw.Flush()

$dest = Join-Path $PSScriptRoot "..\src\KnobEQ.ico"
[IO.File]::WriteAllBytes($dest, $out.ToArray())
Write-Host "アイコンを作りました: $((Resolve-Path $dest).Path) ($($sizes -join ', ') px)"
