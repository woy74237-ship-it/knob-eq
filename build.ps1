# KnobEQ のビルドスクリプト
# Windows に標準で入っている .NET Framework 4 の C# コンパイラを使うので、追加インストールは不要です。
$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "C# コンパイラが見つかりません: $csc" }

Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force bin | Out-Null
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /out:bin\KnobEQ.exe `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll src\KnobEQ.cs
if ($LASTEXITCODE -ne 0) { throw "ビルドに失敗しました" }
Write-Host "ビルド完了: bin\KnobEQ.exe"
