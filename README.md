# KnobEQ

Windows 全体の音に効く、つまみだけのシンプルな 6 バンドイコライザです。
[Equalizer APO](https://sourceforge.net/projects/equalizerapo/) の設定ファイルを書き換えることで動作します。

## 必要なもの

- Windows 10 / 11
- [Equalizer APO](https://sourceforge.net/projects/equalizerapo/)（使う出力デバイスにインストール済みであること）

## 使い方

`KnobEQ.exe` を起動して、つまみを操作します。

| 操作 | 動作 |
|---|---|
| 上下ドラッグ | 値を変更（Shift を押しながらで微調整） |
| マウスホイール | 0.5 dB ずつ変更 |
| ダブルクリック | 0 dB に戻す |
| EQ ON | オフにするとバイパス（聴き比べ用） |
| Reset | すべて 0 dB に戻す |

設定はアプリを閉じても有効なままで、次回起動時に前回の値が復元されます。

## 各つまみの特性

| つまみ | フィルター | 周波数 |
|---|---|---|
| Sub | ピーキング (Q 1.0) | 40 Hz |
| Bass | ローシェルフ | 100 Hz |
| Low-Mid | ピーキング (Q 1.0) | 300 Hz |
| Mid | ピーキング (Q 1.0) | 1 kHz |
| High-Mid | ピーキング (Q 1.0) | 3 kHz |
| Treble | ハイシェルフ | 8 kHz |

範囲は ±12 dB。ブーストした分だけ自動でプリアンプを下げ、クリップ（音割れ）を防ぎます。

## 仕組みと、PC に加える変更

- Equalizer APO の config フォルダに `knobeq.txt` を作成し、つまみを動かすたびに書き換えます。
- `config.txt` に `Include: knobeq.txt` の 1 行を追加します（`Include: peace.txt` があればその直後）。
  初回のみ元のファイルを `config.txt.knobeq-backup` として保存します。
- Peace など他の設定（`peace.txt` など）には触れません。

### 元に戻す（アンインストール）

1. `config.txt` から `Include: knobeq.txt` の行を削除する
2. `knobeq.txt` を削除する
3. `KnobEQ.exe` を削除する

## ビルド

Windows に標準で入っている C# コンパイラを使うので、追加インストールは不要です。

```powershell
.\build.ps1
```

`bin\KnobEQ.exe` が作られます。

## 注意

- WASAPI 排他モードで再生しているアプリの音には EQ はかかりません（Equalizer APO の仕様）。
- 実行ファイルには署名がないため、初回起動時に Windows SmartScreen の警告が出ることがあります。

## ライセンス

[MIT License](LICENSE)
