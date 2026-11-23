# 紹介ページと記事用の画像を作る
#   site\images\icon-256.png / apple-touch-icon.png / favicon-32.png … アイコン（src\KnobEQ.ico から）
#   site\images\ogp.png（1200x630）                                    … SNS で共有されたときのサムネイル
#   promo\note-header.png（1280x670）                                  … note の見出し画像
# 先に tools\make-screenshots.ps1 で site\images\main.png を作っておいてください。
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$site = Join-Path $root 'site\images'
$promo = Join-Path $root 'promo'
New-Item -ItemType Directory -Force $site, $promo | Out-Null
$ico = Join-Path $root 'src\KnobEQ.ico'

function Save-IconPng($size, $path) {
    $icon = New-Object Drawing.Icon $ico, 256, 256
    $src = $icon.ToBitmap()
    $bmp = New-Object Drawing.Bitmap $size, $size
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $size, $size); $g.Dispose()
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
}
Save-IconPng 256 (Join-Path $site 'icon-256.png')
Save-IconPng 180 (Join-Path $site 'apple-touch-icon.png')
# 32px は縮小ではなく、32px 用に描き分けた画像をそのまま使う
(New-Object Drawing.Icon $ico, 32, 32).ToBitmap().Save((Join-Path $site 'favicon-32.png'), [Drawing.Imaging.ImageFormat]::Png)

# アイコン・アプリ名・説明・画面写真を並べたバナー
function Save-Banner($w, $h, $path, $lang = 'ja') {
    $en = $lang -eq 'en'
    $bmp = New-Object Drawing.Bitmap $w, $h
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Rectangle 0, 0, $w, $h),
        ([Drawing.Color]::FromArgb(0x16, 0x1E, 0x27)), ([Drawing.Color]::FromArgb(0x24, 0x33, 0x40)), 30
    $g.FillRectangle($bg, 0, 0, $w, $h)

    $k = $h / 630.0
    $x = [int](72 * $k)
    $icon = (New-Object Drawing.Icon $ico, 256, 256).ToBitmap()
    $g.DrawImage($icon, $x, [int](120 * $k), [int](112 * $k), [int](112 * $k))

    $white = [Drawing.Brushes]::White
    $dim = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(0xC5, 0xD3, 0xDC))
    $blue = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(0x4C, 0xC2, 0xFF))
    $g.DrawString('KnobEQ', (New-Object Drawing.Font 'Segoe UI', (54 * $k), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)), $white, $x - 4, [int](250 * $k))
    $f2 = New-Object Drawing.Font 'Yu Gothic UI', (34 * $k), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $g.DrawString($(if ($en) { 'Turn a knob.' } else { 'つまみを回すだけで、' }), $f2, $white, $x, [int](340 * $k))
    $g.DrawString($(if ($en) { 'Make Windows sound right.' } else { 'Windows の音を好みに。' }), $f2, $white, $x, [int](388 * $k))
    $f3 = New-Object Drawing.Font 'Yu Gothic UI', (22 * $k), ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
    $g.DrawString($(if ($en) { 'Equalizer for Equalizer APO  ·  Free  ·  No install' } else { 'Equalizer APO 用イコライザ ・ 無料 ・ インストール不要' }), $f3, $dim, $x, [int](460 * $k))
    $g.DrawString($(if ($en) { 'Includes a goal-based tuning guide' } else { '迷ったら、目的から選べるチューニングガイド付き' }), $f3, $blue, $x, [int](496 * $k))

    # 画面写真（右側）
    $shot = [Drawing.Image]::FromFile((Join-Path $site $(if ($en) { 'en\main.png' } else { 'main.png' })))
    $sw = [int](560 * $k); $sh = [int]($shot.Height * $sw / $shot.Width)
    $sx = $w - $sw - [int](56 * $k); $sy = [int](($h - $sh) / 2)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(70, 0, 0, 0))), $sx + 6, $sy + 10, $sw, $sh)
    $g.DrawImage($shot, $sx, $sy, $sw, $sh)
    $g.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(0x3E, 0x4C, 0x59)), 1), $sx, $sy, $sw - 1, $sh - 1)
    $shot.Dispose(); $g.Dispose()
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    Write-Host "作成: $path ($w x $h)"
}
Save-Banner 1200 630 (Join-Path $site 'ogp.png')
Save-Banner 1280 670 (Join-Path $promo 'note-header.png')
Save-Banner 1200 630 (Join-Path $site 'en\ogp.png') 'en'
