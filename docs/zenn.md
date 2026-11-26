---
title: "Equalizer APO を「つまみ」で操作する Windows アプリを作った（補正カーブの計算と「効かない」の検出）"
emoji: "🎛️"
type: "tech"
topics: ["csharp", "windows", "winforms", "equalizerapo", "個人開発"]
published: false
---

<!--
  投稿メモ（このコメントは表示されません）
  ・画像 3 枚（docs/images/main.png, guide.png, warning.png）をエディタにドラッグ＆ドロップしてアップロードし、
    下の ![...](images/...) をアップロード後の URL に差し替えてください（tools/make-screenshots.ps1 で生成）。
  ・@[card] のリンクは、KnobEQ の紹介ページとリポジトリを公開してからでないと表示されません。
  ・準備ができたら上の published を true にして公開します（Web エディタなら「公開する」ボタン）。
  ・Web エディタでは上の title / emoji / topics は使われないので、「公開設定」で
    絵文字 🎛️、トピック（csharp, windows, winforms, equalizerapo, 個人開発）、カテゴリー Tech を設定してください。
  ・太字の閉じの ** の直前が「）」などの記号で、直後が文字だと、太字にならず ** がそのまま表示されます。
-->

Windows 全体の音にイコライザをかけるなら、定番は **Equalizer APO** です。ただ、設定は基本的にテキストファイルに数値を書く形で、GUI の Peace も高機能なぶん初めての人には少し難しい。そこで、**つまみを回すだけで Equalizer APO を操作できる** アプリ「KnobEQ」を作りました。

![KnobEQ の画面。補正カーブのグラフと 6 つのつまみ](images/main.png)

以前作った「[壁紙スケジューラ](https://zenn.dev/walt_owen/articles/3291d8d844a05d)」「[輝度スケジューラ](https://zenn.dev/walt_owen/articles/4b5cab6cb4d673)」と同じく、Windows 標準の `csc.exe` だけでビルドしている 1 ファイルの WinForms アプリです。この記事では、Equalizer APO との連携、補正カーブの計算、そして「EQ が効かない」原因の検出について書きます。

## できること

- つまみ（上下ドラッグ・ホイール）で、Windows 全体の音を調整
- 3 ノブ（Treble / Mid / Bass）と 6 ノブ（Sub〜Treble）の切り替え
- 補正カーブのグラフと、音割れを防ぐ自動プリアンプ
- 「曇りを取りたい」などの**目的から選べるチューニングガイド**
- 「オーディオの機能強化」がオフなど、**EQ が効かない状態の検出**
- ライト／ダークモード、日本語／英語の切り替え

## ダウンロード

@[card](https://woy74237-ship-it.github.io/knob-eq/)

インストール不要で、ZIP を展開して exe を起動するだけで使えます（Windows 10 / 11、Equalizer APO が必要）。ソースコードは MIT ライセンスで公開しています。

@[card](https://github.com/woy74237-ship-it/knob-eq)

---

ここからは作り方の話です。

## Equalizer APO には「1 行足して、自分のファイルを書く」だけ

Equalizer APO は、インストール先の `config\config.txt` を読み込んで音を処理します。ファイルが変わると自動で読み直すので、アプリ側は**ファイルを書き換えるだけ**で音が変わります。

ただ、`config.txt` を丸ごと書き換えると、Peace などほかのツールの設定を壊してしまいます。そこで KnobEQ は、

1. `config.txt` に `Include: knobeq.txt` の **1 行だけ**を追加し（初回だけバックアップを取る）
2. あとは自分専用の `knobeq.txt` だけを書き換える

という形にしました。`Include: peace.txt` があればその直後に入れるので、Peace の補正のあとに KnobEQ の味付けが重なります。

```
# KnobEQ が自動生成するファイルです（手で編集しても上書きされます）
# state: Treble=3.0 High-Mid=1.5 Mid=0.0 Low-Mid=-2.0 Bass=4.0 Sub=3.0 3:Treble=3.0 3:Mid=-2.0 3:Bass=5.0 mode=6 layout=h on
Device: all
Channel: all
Preamp: -4.4 dB
Filter: ON PK Fc 40 Hz Gain 3.0 dB Q 1.0
Filter: ON PK Fc 100 Hz Gain 4.0 dB Q 1.0
Filter: ON PK Fc 300 Hz Gain -2.0 dB Q 1.0
...
Filter: ON HS Fc 8000 Hz Gain 3.0 dB
```

つまみの値やモードは、2 行目のコメントに書いておいて、起動時にそこから復元しています。設定ファイルを別に持たずに済むので、アンインストールは「1 行消して、ファイルを 2 つ消す」だけです。

書き込みは、つまみを動かしている間に連続して起きないよう、最後の操作から 120 ms 待ってから行っています。

:::message
Equalizer APO が設定をどう解釈したかは、付属の `Benchmark.exe` に `-v` を付けると確認できます。フィルターの種類や Q の値まで、読み込んだ内容がそのまま表示されるので、書式の確認にとても便利でした。

```
Benchmark.exe -l 2 -r 48000 -v --nopause
(TRACE) Adding low-shelf filter with frequency 100 Hz and gain 3 dB and Q 0.5
```
:::

## 3 ノブは「なだらかに」、6 ノブは「山」を並べる

6 ノブモードは、Sub（40 Hz）から High-Mid（3 kHz）までを Q 1.0 のピーキング、Treble（8 kHz）だけをハイシェルフにしています。

3 ノブモードは 3 つで全帯域を受け持つので、同じ設定だと間に効きの薄いところができます。そこで、Bass / Treble は**傾きをゆるやかにしたシェルフ（Q 0.5）**、Mid は**幅の広いピーキング（Q 0.7）**にしました。Equalizer APO では、Q を指定するシェルフは `LS` / `HS` ではなく `LSC` / `HSC` と書きます。

```
Filter: ON LSC Fc 100 Hz Gain 6.0 dB Q 0.5
Filter: ON PK Fc 1000 Hz Gain -3.0 dB Q 0.7
Filter: ON HSC Fc 8000 Hz Gain 3.0 dB Q 0.5
```

## 補正カーブは RBJ の式でそのまま計算する

グラフは、各フィルターの係数を **RBJ Audio EQ Cookbook** の式で求め、周波数応答の大きさを足し合わせて描いています。たとえばピーキングはこうです。

```csharp
double A = Math.Pow(10, gainDb / 40), w0 = 2 * Math.PI * freq / Fs;
double al = Math.Sin(w0) / (2 * q), c = Math.Cos(w0);
double b0 = 1 + al * A, b1 = -2 * c, b2 = 1 - al * A;
double a0 = 1 + al / A, a1 = -2 * c, a2 = 1 - al / A;

// H(e^jw) の大きさ（dB）
double w = 2 * Math.PI * f / Fs;
double nr = b0 + b1 * Math.Cos(w) + b2 * Math.Cos(2 * w), ni = -(b1 * Math.Sin(w) + b2 * Math.Sin(2 * w));
double dr = a0 + a1 * Math.Cos(w) + a2 * Math.Cos(2 * w), di = -(a1 * Math.Sin(w) + a2 * Math.Sin(2 * w));
return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di));
```

横軸は 20 Hz〜20 kHz の対数で、グラフの幅の 1 ピクセルごとに計算しています。フィルターは最大 6 個なので、毎回計算しても十分軽いです。

## プリアンプは「つまみの最大値」ではなく「カーブの最大値」で決める

音を持ち上げると、元の音がフルスケールに近い場合にクリップ（音割れ）します。そこで、持ち上げた分だけ全体を下げる `Preamp` を自動で入れています。

最初は「いちばん上げているつまみの値」だけ下げていましたが、これだと**隣り合うつまみの効きが重なる**と足りません。Bass と Sub を両方 +12 dB にすると、重なった部分は +20 dB を超えます。

そこで、グラフと同じ計算で 20 Hz〜20 kHz のカーブの最大値を求め、その分を 0.1 dB 単位で切り上げて下げるようにしました。各つまみの中心周波数は、ピークを取りこぼさないよう必ず計算に含めています。

```csharp
double peak = 0;
for (int i = 0; i <= 400; i++) peak = Math.Max(peak, SumDb(FMin * Math.Pow(FMax / FMin, i / 400.0)));
foreach (Band b in Bands) peak = Math.Max(peak, SumDb(b.Freq));
float preamp = -(float)(Math.Ceiling(peak * 10 - 1e-6) / 10);
```

## 「EQ が効かない」を検出する

作っている途中で、突然 EQ が効かなくなりました。KnobEQ が書いたファイルも、Equalizer APO が読み込んだ内容（`Benchmark.exe -v`）も正しいのに、音が変わらない。

原因は、Windows の **「オーディオの機能強化」がオフ** になっていたことでした。オフにすると、Windows は Equalizer APO を含む音響効果（APO）をすべて外します。Windows の更新や Bluetooth の再接続で、いつの間にかオフになっていることがあります。

これはユーザーには原因が分かりにくいので、アプリで検出して知らせることにしました。

1. **既定の出力デバイス**の ID を `IMMDeviceEnumerator.GetDefaultAudioEndpoint` で取得
2. `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{デバイスの GUID}\FxProperties` を読む
3. 値の中に Equalizer APO の CLSID（`{EACD2258-FCAC-4FF4-B36D-419E924A6D79}`）があるか → 無ければ「このデバイスには Equalizer APO が入っていない」
4. `{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5`（`PKEY_AudioEndpoint_Disable_SysFx`）が 1 なら「オーディオの機能強化がオフ」

（要点だけを抜き出して短くしています）

```csharp
using (RegistryKey fx = Registry.LocalMachine.OpenSubKey(RenderKey + guid + @"\FxProperties"))
{
    bool hasApo = fx.GetValueNames().Any(n => Convert.ToString(fx.GetValue(n)).Contains(ApoClsid));
    if (!hasApo) return Status.NoApo;
    object off = fx.GetValue("{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5");
    if (off is int && (int)off != 0) return Status.EnhancementsOff;
    return Status.Ok;
}
```

出力先の切り替えや設定の変更に気づけるよう、3 秒ごとに調べ直しています。注意をクリックすると、`ms-settings:sound`（サウンド設定）や Equalizer APO の Configurator が開きます。

![「オーディオの機能強化」がオフのときの注意](images/warning.png)

:::message
音楽アプリが WASAPI の**排他モード**で再生しているときも、APO を通らないので EQ は効きません。こちらはアプリ側の設定なので、KnobEQ からは検出できず、README に書くだけにしています。
:::

## 目的から選べるチューニングガイド

イコライザのいちばんの壁は、「どこを、どれだけ」が分からないことだと思っています。そこで、「曇りを取りたい」「重低音の振動を感じたい」のような**ふだんの言葉の目的**を 8 つ用意し、選ぶと回すつまみと目安を案内するようにしました。

![チューニングガイド](images/guide.png)

- 目安のあるつまみは、輪の上に目安の範囲を弧で描き、名前を太字にする
- グラフには、目安どおりに回したときのカーブを点線で重ねる（上と同じ計算で、目安の値に置き換えて足すだけ）
- 「目安を当てはめる」「元に戻す」「聴き比べ（EQ の ON/OFF）」

目安は 3 ノブ用と 6 ノブ用を別々に持っています。3 ノブには Low-Mid などがないので、同じ「曇りを取りたい」でも動かすつまみが変わるためです。

## そのほか

- **ライト／ダークモード**: `HKCU\...\Themes\Personalize\AppsUseLightTheme` で判定し、アクセントカラーは `Explorer\Accent\AccentPalette` から、ダークでは明るめ・ライトでは暗めの色を取っています。タイトルバーは `DwmSetWindowAttribute(hwnd, 20, ...)`（`DWMWA_USE_IMMERSIVE_DARK_MODE`）。`SystemEvents.UserPreferenceChanged` で、設定を変えたらすぐ塗り直します。
- **日本語／英語**: 文言を `L.T("日本語", "English")` のように並べて書き、`CultureInfo.CurrentUICulture` で切り替えています。翻訳ファイルを持たないので、1 ファイルのまま保てます。

## おわりに

「なんとなく物足りない」と思っていた音が、つまみを少し回すだけで好みの音になるのは、やっぱり楽しいです。Equalizer APO を入れたけれど使いこなせなかった、という方にもぜひ試してみてほしいです。

不具合の報告や「こんな目的も選べるようにしてほしい」という要望は、GitHub の Issues でお待ちしています。

このアプリが気に入っていただけたら、[OFUSE](https://ofuse.me/c0fdccc4) か [Ko-fi](https://ko-fi.com/waltowen404923) から開発を応援していただけるとうれしいです。
