# KnobEQ

Windows 全体の音を変えられる無料ソフト [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) を、つまみを回すだけで操作できるようにしたアプリです（3 ノブ / 6 ノブ）。
「曇りを取りたい」「重低音の振動を感じたい」のような目的から選べる、チューニングガイド付き。
音を実際に変えているのは Equalizer APO で、KnobEQ はその設定ファイルを書き換えて操作します。

A simple knob-style front end for Equalizer APO, the free system-wide equalizer for Windows. → [English](#english)

![KnobEQ の画面](site/images/main.png)

画面は Windows の表示言語に合わせて、日本語または英語で表示されます。

## ダウンロード

**[紹介ページ](https://woy74237-ship-it.github.io/knob-eq/)** または **[Releases](https://github.com/woy74237-ship-it/knob-eq/releases/latest)** から ZIP をダウンロードし、展開した `KnobEQ.exe` を起動します。
インストールは不要です。

使い方の紹介記事: [イコライザ設定に疲れた方へ。目的から選べる無料の EQ アプリ「KnobEQ」を作りました（note）](https://note.com/quiet_raven5890/n/n99003c7af137)
作り方の解説記事: [Equalizer APO を「つまみ」で操作する Windows アプリを作った（Zenn）](https://zenn.dev/walt_owen/articles/c442483d41bf54)

初回起動時に「Windows によって PC が保護されました」と表示されたら、「詳細情報」→「実行」で起動できます（署名のない個人製アプリで表示される確認画面です）。

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
| ヒント | チューニングガイドを開く／閉じる |
| Reset | 今のモードのつまみをすべて 0 dB に戻す |
| 右上の 3 / 6 | 3 ノブモードと 6 ノブモードを切り替える |
| 右上の ↔ / ↕ | 横レイアウトと縦レイアウトを切り替える |

設定はアプリを閉じても有効なままで、次回起動時に前回の値・モード・レイアウトが復元されます。

### 縦レイアウトと横レイアウト

- **縦**：グラフの下に、つまみを 1 列に並べます（上から高音 → 低音）。
- **横**：グラフの下に、つまみを 3 つずつ並べます。6 ノブでは 2 段、3 ノブでは 1 段になります。
  つまみはグラフの横軸と同じ向きで、左上から低音 → 高音の順に並びます。

画面の色は Windows の設定（ライト／ダークモードとアクセントカラー）に合わせて自動で変わります。
アプリを開いたまま Windows の設定を変えても、すぐに切り替わります。

### チューニングガイド

「ヒント」を押すと、本体の横にチューニングガイドが開きます。
「曇りを取りたい」のような目的を選ぶと、どのつまみをどちらにどれくらい回せばよいかを案内します。

- 理由（どの帯域が関係しているか）と、やりすぎないためのコツを表示します。
- 目安のあるつまみは、本体で名前が太字になり、輪の上に目安の範囲が表示されます。
- グラフには、目安どおりに回したときのカーブを点線で重ねます。
- 「目安を当てはめる」で、目安の真ん中の値につまみをまとめて動かします。「元に戻す」で、当てはめる前の値に戻せます。
- 「聴き比べ」で EQ の ON / OFF を切り替え、補正のありなしを比べられます。
- 目安は 3 ノブ / 6 ノブモードそれぞれに用意してあり、モードを切り替えると表示も変わります。

| 目的 | 6 ノブの目安 | 3 ノブの目安 |
|---|---|---|
| 曇りを取りたい | Low-Mid −2〜−4、High-Mid +1 | Bass −1、Treble +2 |
| 華やかにしたい | Treble +2〜+4、High-Mid +1 | Treble +3、Mid +1 |
| 空気感を感じたい | Treble +2〜+3、High-Mid −1 | Treble +3、Mid −1 |
| 重低音の振動を感じたい | Sub +4〜+6、Bass +1〜+2、Low-Mid −1 | Bass +4〜+6 |
| ボーカルを前に出したい | High-Mid +2、Mid +1、Low-Mid −1 | Mid +2 |
| 刺さる音をやわらげたい | High-Mid −2〜−3、Treble −1〜−2 | Treble −2、Mid −1 |
| 音に厚みがほしい | Low-Mid +2、Bass +1 | Bass +2、Mid +1 |
| 小さい音量でも迫力がほしい | Sub +2、Bass +2、Treble +2 | Bass +3、Treble +2 |

目安はあくまで出発点です。当てはめたあとは、聴きながら好みに合わせて調整してください。

### EQ が効かないときの注意表示

今の出力先で EQ が効かない状態になっていると、グラフの上に注意を表示します。
出力先の切り替えや設定の変更は数秒ごとに調べ直し、直ると注意は自動で消えます。

| 注意 | 原因 | 直し方（注意をクリックすると開きます） |
|---|---|---|
| 「オーディオの機能強化」がオフ | Windows が音響効果をすべて止めている | 「設定」→「システム」→「サウンド」で出力先を選び、「オーディオの機能強化」を「デバイスの既定の効果」にする |
| Equalizer APO が入っていない | その出力先に Equalizer APO がインストールされていない | Equalizer APO の Configurator（DeviceSelector.exe）でその出力先にチェックを入れる |

### 補正カーブのグラフ

上部のグラフに、今のつまみで音がどう変わるか（周波数ごとに何 dB 持ち上げ／下げているか）を表示します。

- 横軸は周波数（20 Hz〜20 kHz）、縦軸はゲイン（dB）です。
- 効いているつまみをすべて合わせたカーブを描きます。3 ノブモードでは表示中の 3 つだけです。
- 縦軸は ±12 dB が基本で、つまみを重ねてそれを超えたときは自動で広がります。
- プリアンプ（音割れ防止で全体を下げる分）はカーブには含めず、グラフの右上に数値で表示します。
  カーブがいちばん高くなっているところ（20 Hz〜20 kHz）の分だけ、全体を自動で下げます。
  Bass と Sub のように効きが重なるつまみを両方上げたときも、重なった分まで含めて下げます。
- カーブはアプリ内で計算したもので、Equalizer APO の実際の特性とは細部が少し異なる場合があります。

### 3 ノブモードと 6 ノブモード

2 つのモードは、それぞれ独立したイコライザとして動きます。

- **6 ノブモード**：下の表の 6 バンドすべてを使います。
- **3 ノブモード**：Treble / Mid / Bass の 3 つだけを使います。
  3 つで全帯域を受け持つため、Bass と Treble はシェルフの傾きをゆるやかに（Q 0.5）、
  Mid は効く幅を広く（Q 0.7）しています。
  6 ノブモードの Bass は 100 Hz を中心にした山の形で、それより低い帯域は Sub が受け持ちます。
  3 ノブモードの Bass は、低音全体をまとめて持ち上げる棚の形（ローシェルフ）です。
- つまみの値はモードごとに別々に保存されます。切り替えると、そのモードで前回設定した値に戻ります。
  一方のモードでつまみを回したり Reset したりしても、もう一方のモードの値は変わりません。

## 各つまみの特性

| つまみ | フィルター | 周波数 | 3 ノブモード |
|---|---|---|---|
| Sub | ピーキング (Q 1.0) | 40 Hz | 使わない |
| Bass | ピーキング (Q 1.0) | 100 Hz | ○（ローシェルフ Q 0.5） |
| Low-Mid | ピーキング (Q 1.0) | 300 Hz | 使わない |
| Mid | ピーキング (Q 1.0) | 1 kHz | ○（Q 0.7） |
| High-Mid | ピーキング (Q 1.0) | 3 kHz | 使わない |
| Treble | ハイシェルフ | 8 kHz | ○（Q 0.5） |

範囲は ±12 dB。持ち上げた分（つまみ同士の重なりも含む）だけ自動でプリアンプを下げ、クリップ（音割れ）を防ぎます。

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

`bin\KnobEQ.exe` が作られます。アイコン（`src\KnobEQ.ico`）も exe に埋め込まれます。

アイコンのデザインを変えたときは、次のコマンドで `src\KnobEQ.ico` を作り直してからビルドします。

```powershell
.\tools\make-icon.ps1
```

## 注意

- WASAPI 排他モードで再生しているアプリの音には EQ はかかりません（Equalizer APO の仕様）。
  この場合は注意表示も出ないので、アプリ側の出力設定を確認してください。
- 実行ファイルには署名がないため、初回起動時に Windows SmartScreen の警告が出ることがあります。

## 開発の支援
。気に入っていただけたら、[OFUSE](https://ofuse.me/c0fdccc4) から応援していただけるとうれしいです（海外からは [Ko-fi](https://ko-fi.com/waltowen404923) もご利用いただけます）。

## ライセンス

[MIT License](LICENSE)

---

## English

KnobEQ is a simple, knob-style front end for [Equalizer APO](https://sourceforge.net/projects/equalizerapo/), the free system-wide equalizer for Windows.
Equalizer APO does the actual audio processing; KnobEQ controls it by writing its configuration file, so you can shape the sound of your whole PC just by turning a few knobs.
The app follows your Windows display language (Japanese or English).

![KnobEQ](site/images/en/main.png)

### Features

- **Just turn the knobs** — drag up/down or scroll; double-click to reset to 0 dB
- **Tuning guide** — pick what you want ("Remove muddiness", "Feel the deep bass rumble", …) and KnobEQ highlights which knobs to turn and by how much, with the target curve on the graph
- **3 or 6 knobs** — a simple tone control (Treble / Mid / Bass) or six bands (Sub to Treble); each mode remembers its own settings
- **Response graph and automatic preamp** — see what your settings do, and avoid clipping (overlapping bands are taken into account)
- **Tells you why it isn't working** — detects when "Audio enhancements" are off or Equalizer APO isn't installed for the current output device
- **Vertical or horizontal layout**, light / dark mode and accent color that follow Windows
- Works alongside Peace and other Equalizer APO front ends

![Tuning guide](site/images/en/guide.png)

### Requirements

- Windows 10 / 11 (.NET Framework 4.x is built in)
- [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) installed for your headphones or speakers (select them in the Configurator)

### Getting started

1. Install Equalizer APO and select your output devices in its Configurator.
2. Download the ZIP from [Releases](https://github.com/woy74237-ship-it/knob-eq/releases/latest) and extract it.
3. Run `KnobEQ.exe` and turn the knobs. Settings stay active after you close the app.

If Windows shows "Windows protected your PC", click "More info" → "Run anyway" (the app isn't code-signed).

### What it changes on your PC

- Creates `knobeq.txt` in the Equalizer APO config folder and rewrites it as you turn the knobs.
- Adds one line, `Include: knobeq.txt`, to `config.txt` (a backup is saved once as `config.txt.knobeq-backup`).
- Other files such as `peace.txt` are left untouched.

To uninstall, remove the `Include: knobeq.txt` line from `config.txt`, delete `knobeq.txt`, and delete the KnobEQ folder.

### Support

KnobEQ is free. If you enjoy it, you can [buy me a coffee on Ko-fi](https://ko-fi.com/waltowen404923). Thank you!

### License

[MIT License](LICENSE)
