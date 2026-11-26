KnobEQ
======

A simple, knob-style front end for "Equalizer APO", the free system-wide
equalizer for Windows. Equalizer APO does the actual audio processing;
KnobEQ controls it, so you can shape the sound of your whole PC just by
turning a few knobs.

The app follows your Windows display language (Japanese or English).


* Requirements

  Windows 10 / Windows 11
  (.NET Framework 4.x is built into Windows, so nothing else to install)

  Equalizer APO (free) must be installed for the output device you use
  (headphones or speakers).
    https://sourceforge.net/projects/equalizerapo/


* Getting started

  1. Install Equalizer APO.
     In the "Configurator" that appears during setup, select the headphones
     and speakers you normally use (you can change this later).
  2. Right-click the KnobEQ ZIP and choose "Extract All".
  3. Double-click KnobEQ.exe and turn the knobs.

  Changes are applied immediately.
  Your settings stay active even after you close the app.


* If Windows shows "Windows protected your PC"

  This screen appears for unsigned apps downloaded from the internet.
  Click "More info", then "Run anyway".


* Controls

  - Drag up/down        ... change the value (hold Shift for fine control)
  - Mouse wheel         ... change by 0.5 dB
  - Double-click        ... reset to 0 dB
  - EQ ON               ... turn off to bypass the EQ (for A/B comparison)
  - Reset               ... reset all knobs in the current mode to 0 dB
  - 3 / 6 (top right)   ... switch between 3 knobs (Treble / Mid / Bass) and 6 knobs
  - Arrows (top right)  ... switch between horizontal and vertical layout
  - Heart (top right)   ... open the support page (Ko-fi)
  - Guide               ... open the tuning guide

  The graph shows how your current settings change the sound.
  "Preamp" (top right of the graph) is how much the overall volume is lowered
  automatically to prevent clipping.


* Tuning guide

  Click "Guide" and pick what you want, such as "Remove muddiness" or
  "Feel the deep bass rumble". KnobEQ shows which knobs to turn and by how much.
  "Apply suggestion" moves the knobs to the suggested values at once.


* If the sound doesn't change

  If a notice appears in the app, click it to open the settings that fix it.

  - "Audio enhancements are turned off"
      Open Windows Settings > System > Sound, select your output device,
      and set "Audio enhancements" to "Device Default Effects".
  - "Equalizer APO isn't installed"
      Select that output device in the Equalizer APO Configurator.

  The EQ also has no effect when a music app plays in "exclusive mode".
  Check the output settings of that app.


* What KnobEQ changes on your PC

  KnobEQ makes the following changes in the Equalizer APO config folder
  (usually C:\Program Files\EqualizerAPO\config):

  - Creates knobeq.txt and rewrites it whenever you turn a knob.
    Your knob values and mode are also saved in this file.
  - Adds one line, "Include: knobeq.txt", to config.txt.
    The original file is saved once as config.txt.knobeq-backup.
  - Settings of other tools, such as Peace, are left untouched.


* Uninstall

  1. Remove the line "Include: knobeq.txt" from config.txt.
  2. Delete knobeq.txt.
  3. Delete the extracted KnobEQ folder.

  KnobEQ has no installer, so nothing else is left behind.


* Support

  KnobEQ is free. If you enjoy it, a coffee would make my day.

    https://ko-fi.com/waltowen404923


* Official pages

  https://woy74237-ship-it.github.io/knob-eq/en/
  https://github.com/woy74237-ship-it/knob-eq
