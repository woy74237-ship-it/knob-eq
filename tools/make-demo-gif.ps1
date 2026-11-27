# 紹介用のデモ GIF（英語の画面）を作る
#   site\images\en\demo.gif に保存します（README や海外向けの投稿で使う）
# 見た目確認用のビルド（UI_TEST）をダーク表示・既定の青・英語で起動し、
# チューニングガイドで目的を選ぶ → 目安を当てはめる → つまみを回す、の流れを撮影します。
# 撮影中は Equalizer APO の knobeq.txt を書き換えるので、終わったら元に戻します。
#   使い方: .\tools\make-demo-gif.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$eqFile = 'C:\Program Files\EqualizerAPO\config\knobeq.txt'
$work = Join-Path $root 'bin\uitest'
$frames = Join-Path $root 'bin\demo-frames'
$out = Join-Path $root 'site\images\en\demo.gif'
New-Item -ItemType Directory -Force $work | Out-Null
if (Test-Path $frames) { Remove-Item -Recurse -Force $frames }
New-Item -ItemType Directory -Force $frames | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $work 'KnobEQ.exe'
& $csc /nologo /codepage:65001 /target:winexe /define:UI_TEST "/out:$exe" `
    "/resource:$root\src\KnobEQ.ico,KnobEQ.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$root\src\KnobEQ.cs"
if ($LASTEXITCODE -ne 0) { throw "ビルドに失敗しました" }

Add-Type -AssemblyName System.Drawing
# GIF の書き出しは、.NET Framework の csc でビルドした小さな道具で行う
$gifTool = Join-Path $work 'GifWriter.exe'
& $csc /nologo /target:exe /optimize+ "/out:$gifTool" /r:System.Drawing.dll (Join-Path $PSScriptRoot 'GifWriter.cs')
if ($LASTEXITCODE -ne 0) { throw "GifWriter のビルドに失敗しました" }
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public class Demo {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public struct R { public int L,T,Rr,B; }
}
'@
[Demo]::SetProcessDPIAware() | Out-Null
$screenScale = ([Drawing.Graphics]::FromHwnd([IntPtr]::Zero)).DpiX / 96.0

function Get-Window($procId, $title) {
  $script:hit = [IntPtr]::Zero
  [Demo]::EnumWindows({ param($h,$l) $p = 0; [Demo]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($p -eq $procId -and [Demo]::IsWindowVisible($h)) { $sb = New-Object Text.StringBuilder 256; [Demo]::GetWindowText($h, $sb, 256) | Out-Null
      if ($sb.ToString() -eq $title) { $script:hit = $h; return $false } }; $true }, [IntPtr]::Zero) | Out-Null
  $script:hit
}
function Find-Child($parent, [scriptblock]$match) {
  $script:found = [IntPtr]::Zero
  [Demo]::EnumChildWindows($parent, { param($h,$l)
    $t = New-Object Text.StringBuilder 256; [Demo]::GetWindowText($h, $t, 256) | Out-Null
    $c = New-Object Text.StringBuilder 256; [Demo]::GetClassName($h, $c, 256) | Out-Null
    if (& $match $t.ToString() $c.ToString()) { $script:found = $h; return $false }; $true }, [IntPtr]::Zero) | Out-Null
  $script:found
}
function Click($h) { [Demo]::SendMessage($h, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 350 }
function Capture($h) {
  $r = New-Object Demo+R; [Demo]::GetWindowRect($h, [ref]$r) | Out-Null
  $full = New-Object Drawing.Bitmap ($r.Rr - $r.L), ($r.B - $r.T)
  $g = [Drawing.Graphics]::FromImage($full); $hdc = $g.GetHdc(); [Demo]::PrintWindow($h, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
  $w = [int][Math]::Round($full.Width / $screenScale); $hh = [int][Math]::Round($full.Height / $screenScale)
  $full.Clone((New-Object Drawing.Rectangle 0, 0, $w, $hh), $full.PixelFormat)
}

$script:frameNo = 0; $script:delays = New-Object System.Collections.ArrayList
# 本体とガイドを横に並べた 1 コマを保存する（delay は 1/100 秒）
function Shot($delay) {
  $a = Capture $script:main; $b = Capture $script:guide; $gap = 24
  $bg = [Drawing.Color]::FromArgb(0x16, 0x1C, 0x22)
  $all = New-Object Drawing.Bitmap ($a.Width + $gap + $b.Width + 32), ([Math]::Max($a.Height, $b.Height) + 32)
  $g = [Drawing.Graphics]::FromImage($all); $g.Clear($bg)
  $g.DrawImage($a, 16, 16, $a.Width, $a.Height); $g.DrawImage($b, 16 + $a.Width + $gap, 16, $b.Width, $b.Height); $g.Dispose()
  $all.Save((Join-Path $frames ('{0:D3}.png' -f $script:frameNo)), [Drawing.Imaging.ImageFormat]::Png)
  $all.Dispose(); $a.Dispose(); $b.Dispose()
  [void]$script:delays.Add($delay); $script:frameNo++
}
# ガイドの一覧で目的を選ぶ（LB_GETITEMRECT で項目の位置を聞き、その中央をクリックする）
function Select-Goal($index) {
  $lb = Find-Child $script:guide { param($t, $c) $c -like '*LISTBOX*' }
  $rect = [Runtime.InteropServices.Marshal]::AllocHGlobal(16)
  try {
    [Demo]::SendMessage($lb, 0x198, [IntPtr]$index, $rect) | Out-Null
    $l = [Runtime.InteropServices.Marshal]::ReadInt32($rect, 0); $t = [Runtime.InteropServices.Marshal]::ReadInt32($rect, 4)
    $b = [Runtime.InteropServices.Marshal]::ReadInt32($rect, 12)
  } finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($rect) }
  # 項目の位置は KnobEQ の座標（100%）で返るが、クリックの座標はこちら（高 DPI 対応）から送ると
  # 拡大率の分だけ縮めて届くので、拡大率を掛けておく
  $y = [int](($t + $b) / 2 * $screenScale); $x = [int](($l + 20) * $screenScale)
  $pos = [IntPtr](($y -shl 16) -bor $x)
  [Demo]::SendMessage($lb, 0x201, [IntPtr]1, $pos) | Out-Null
  [Demo]::SendMessage($lb, 0x202, [IntPtr]::Zero, $pos) | Out-Null
  Start-Sleep -Milliseconds 400
}
# つまみをホイールで回す（1 回で 0.5 dB）
function Turn($knob, $steps) {
  $delta = if ($steps -gt 0) { 120 } else { -120 }
  [Demo]::SendMessage($knob, 0x020A, [IntPtr]([long]$delta -shl 16), [IntPtr]::Zero) | Out-Null
  Start-Sleep -Milliseconds 120
}

$wasRunning = [bool](Get-Process KnobEQ -ErrorAction SilentlyContinue)
Get-Process KnobEQ -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 500
$backup = [IO.File]::ReadAllBytes($eqFile)
try {
  Set-Content $eqFile "# state: Treble=0.0 High-Mid=0.0 Mid=0.0 Low-Mid=0.0 Bass=0.0 Sub=0.0 3:Treble=0.0 3:Mid=0.0 3:Bass=0.0 mode=6 layout=h on" -Encoding utf8
  $env:KNOBEQ_THEME = 'dark'; $env:KNOBEQ_ACCENT = 'default'; $env:KNOBEQ_WARN = 'none'; $env:KNOBEQ_LANG = 'en'
  $p = Start-Process $exe -PassThru; Start-Sleep 2
  $script:main = Get-Window $p.Id 'Knob EQ'
  Click (Find-Child $script:main { param($t, $c) $t -eq 'Guide' })
  $script:guide = Get-Window $p.Id 'Tuning Guide'

  # つまみ（左上から Sub, Bass, Low-Mid, Mid, High-Mid, Treble）
  $knobs = @()
  [Demo]::EnumChildWindows($script:main, { param($h,$l)
    if ([Demo]::IsWindowVisible($h)) { $r = New-Object Demo+R; [Demo]::GetWindowRect($h, [ref]$r) | Out-Null
      $w = $r.Rr - $r.L; if ([Math]::Abs($w - ($r.B - $r.T)) -le 2 -and $w -gt 60) { $script:knobs += [pscustomobject]@{ H = $h; T = $r.T; L = $r.L } } }; $true }, [IntPtr]::Zero) | Out-Null
  $k = $script:knobs | Sort-Object T, L
  if ($k.Count -ne 6) { throw "つまみが 6 つ見つかりません（$($k.Count)）" }
  $sub = $k[0].H; $bass = $k[1].H; $treble = $k[5].H

  Shot 160                                  # 1. ガイドを開いたところ（Remove muddiness）
  Select-Goal 1; Shot 130                   # 2. Add sparkle
  Select-Goal 3; Shot 170                   # 3. Feel the deep bass rumble
  Click (Find-Child $script:guide { param($t, $c) $t -eq 'Apply suggestion' }); Shot 180   # 4. 目安を当てはめる
  for ($i = 0; $i -lt 4; $i++) { Turn $treble 1; Shot 22 }   # 5. 高音を少し足す（+2 dB）
  Shot 120
  for ($i = 0; $i -lt 3; $i++) { Turn $bass 1; Shot 22 }     # 6. Bass をもう少し（+1.5 dB）
  Shot 260
}
finally {
  Remove-Item Env:KNOBEQ_THEME, Env:KNOBEQ_ACCENT, Env:KNOBEQ_WARN, Env:KNOBEQ_LANG -ErrorAction SilentlyContinue
  Get-Process KnobEQ -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 500
  [IO.File]::WriteAllBytes($eqFile, $backup)
  Write-Host "knobeq.txt を撮影前の状態に戻しました"
  if ($wasRunning) { Start-Process (Join-Path $root 'bin\KnobEQ.exe') }
}

$files = @(Get-ChildItem $frames -Filter *.png | Sort-Object Name | ForEach-Object FullName)
$list = Join-Path $frames 'frames.txt'
$lines = for ($i = 0; $i -lt $files.Count; $i++) { "{0}`t{1}" -f $files[$i], $script:delays[$i] }
[IO.File]::WriteAllLines($list, [string[]]$lines)
& $gifTool $out $list
if ($LASTEXITCODE -ne 0) { throw "GIF の書き出しに失敗しました" }
Write-Host ("デモ GIF を作りました: {0}（{1} コマ、{2:N0} KB）" -f $out, $files.Count, ((Get-Item $out).Length / 1KB))
