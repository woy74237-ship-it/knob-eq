# 紹介ページと記事に使う画面写真を撮る
#   docs\images\*.png と site\images\*.png に保存します。
# 見た目確認用のビルド（UI_TEST）をダーク表示・既定の青で起動し、決まった値を入れて撮影します。
# 撮影中は Equalizer APO の knobeq.txt を書き換えるので、終わったら元に戻します。
#   使い方: .\tools\make-screenshots.ps1
#   英語の画面で撮るとき: .\tools\make-screenshots.ps1 -Lang en（docs\images\en と site\images\en に保存）
param([ValidateSet('ja', 'en')][string]$Lang = 'ja')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$eqFile = 'C:\Program Files\EqualizerAPO\config\knobeq.txt'
$sub = if ($Lang -eq 'en') { '\en' } else { '' }
$docs = Join-Path $root "docs\images$sub"
$site = Join-Path $root "site\images$sub"
$work = Join-Path $root 'bin\uitest'
New-Item -ItemType Directory -Force $docs, $site, $work | Out-Null

# 見た目確認用のビルド
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $work 'KnobEQ.exe'
& $csc /nologo /codepage:65001 /target:winexe /define:UI_TEST "/out:$exe" `
    "/resource:$root\src\KnobEQ.ico,KnobEQ.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$root\src\KnobEQ.cs"
if ($LASTEXITCODE -ne 0) { throw "ビルドに失敗しました" }

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public class Shot {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public struct R { public int L,T,Rr,B; }
}
'@
[Shot]::SetProcessDPIAware() | Out-Null

function Get-Window($procId, $title) {
  $script:hit = [IntPtr]::Zero
  [Shot]::EnumWindows({ param($h,$l) $p = 0; [Shot]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($p -eq $procId -and [Shot]::IsWindowVisible($h)) { $sb = New-Object Text.StringBuilder 256; [Shot]::GetWindowText($h, $sb, 256) | Out-Null
      if ($sb.ToString() -eq $title) { $script:hit = $h; return $false } }; $true }, [IntPtr]::Zero) | Out-Null
  $script:hit
}
function Click-Button($parent, $text) {
  $script:btn = [IntPtr]::Zero
  [Shot]::EnumChildWindows($parent, { param($h,$l) $sb = New-Object Text.StringBuilder 256; [Shot]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString() -eq $text) { $script:btn = $h; return $false }; $true }, [IntPtr]::Zero) | Out-Null
  [Shot]::SendMessage($script:btn, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
  Start-Sleep -Milliseconds 800
}
# KnobEQ は高 DPI 非対応なので、PrintWindow は拡大前（100%）の大きさで描く。
# ウィンドウの大きさは拡大後で測れるので、画面の拡大率で割った大きさに切り出す
$screenScale = ([Drawing.Graphics]::FromHwnd([IntPtr]::Zero)).DpiX / 96.0
function Capture($h) {
  $r = New-Object Shot+R; [Shot]::GetWindowRect($h, [ref]$r) | Out-Null
  $full = New-Object Drawing.Bitmap ($r.Rr - $r.L), ($r.B - $r.T)
  $g = [Drawing.Graphics]::FromImage($full); $hdc = $g.GetHdc(); [Shot]::PrintWindow($h, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
  $w = [int][Math]::Round($full.Width / $screenScale); $hgt = [int][Math]::Round($full.Height / $screenScale)
  $full.Clone((New-Object Drawing.Rectangle 0, 0, $w, $hgt), $full.PixelFormat)
}
function Save-Both($bmp, $name) {
  $bmp.Save((Join-Path $docs $name), [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Save((Join-Path $site $name), [Drawing.Imaging.ImageFormat]::Png)
  Write-Host "撮影: $name ($($bmp.Width)x$($bmp.Height))"
}
# 1 つの場面を撮る: 値を書き込んで起動し、$action で操作してから撮影する
function Scene($state, $warn, [scriptblock]$action) {
  Get-Process KnobEQ -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 500
  Set-Content $eqFile "# state: $state on" -Encoding utf8
  $env:KNOBEQ_THEME = 'dark'; $env:KNOBEQ_ACCENT = 'default'; $env:KNOBEQ_WARN = $warn; $env:KNOBEQ_LANG = $Lang
  $p = Start-Process $exe -PassThru; Start-Sleep 2
  $main = Get-Window $p.Id 'Knob EQ'
  & $action $p $main
  Stop-Process -Id $p.Id -Force
}

$wasRunning = [bool](Get-Process KnobEQ -ErrorAction SilentlyContinue)
$backup = [IO.File]::ReadAllBytes($eqFile)
try {
  $six = 'Treble=3.0 High-Mid=1.5 Mid=0.0 Low-Mid=-2.0 Bass=4.0 Sub=3.0'
  $three = '3:Treble=3.0 3:Mid=-2.0 3:Bass=5.0'

  Scene "$six $three mode=6 layout=h" 'none' { param($p, $main) Save-Both (Capture $main) 'main.png' }
  Scene "$six $three mode=3 layout=v" 'none' { param($p, $main) Save-Both (Capture $main) 'three.png' }
  Scene "$six $three mode=3 layout=h" 'off' { param($p, $main) Save-Both (Capture $main) 'warning.png' }

  # ガイド: 本体とガイドを横に並べた 1 枚にする
  Scene "Treble=1.0 High-Mid=0.0 Mid=0.0 Low-Mid=0.0 Bass=2.0 Sub=0.0 $three mode=6 layout=h" 'none' {
    param($p, $main)
    Click-Button $main $(if ($Lang -eq 'en') { 'Guide' } else { 'ヒント' })
    $guide = Get-Window $p.Id $(if ($Lang -eq 'en') { 'Tuning Guide' } else { 'チューニングガイド' })
    $a = Capture $main; $b = Capture $guide
    $gap = 24
    $all = New-Object Drawing.Bitmap ($a.Width + $gap + $b.Width), ([Math]::Max($a.Height, $b.Height))
    $g = [Drawing.Graphics]::FromImage($all); $g.Clear([Drawing.Color]::Transparent)
    $g.DrawImage($a, 0, 0, $a.Width, $a.Height); $g.DrawImage($b, $a.Width + $gap, 0, $b.Width, $b.Height); $g.Dispose()
    Save-Both $all 'guide.png'
  }
}
finally {
  Remove-Item Env:KNOBEQ_THEME, Env:KNOBEQ_ACCENT, Env:KNOBEQ_WARN, Env:KNOBEQ_LANG -ErrorAction SilentlyContinue
  Get-Process KnobEQ -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 500
  [IO.File]::WriteAllBytes($eqFile, $backup)
  Write-Host "knobeq.txt を撮影前の状態に戻しました"
  if ($wasRunning) { Start-Process (Join-Path $root 'bin\KnobEQ.exe') }
}
