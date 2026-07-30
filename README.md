# The Forgotten Concluder -- English Patch Backup

    !! MACHINE-TRANSLATED FAN PATCH !!
    Translations are MTL with minor polish on high-frequency UI and dialogue strings. 
	Quality is "playable", tone and idiom will occasionally read as awkward or literal.

    !! PLEASE SUPPORT THE ORIGINAL GAME !!
    The Forgotten Concluder by Kunpo Games  (Published by Kunpo Games, 方块游戏(CubeGame)): https://store.steampowered.com/app/2159730/The_Forgotten_Concluder/


## What is inside

Mod files, translation caches, plugin source, and configs.
No game files (no .exe, no Data folder, no GameAssembly.dll, no i18n.sqlite, no extracted game fonts).

## Apply after a Steam download

1. Extract this zip on top of your Steam game folder. Creates:
  - BepInEx/  
	- dotnet/  
	- _translation/  
	- TextFit_plugin_source/
  -  winhttp.dll  
	- doorstop_config.ini  
	- .doorstop_version  
	- changelog.txt

2. Regenerate i18n.sqlite (English text DB) - AUTOMATIC
     a. Copy Steam's fresh "ForgottenConcluder_Data/StreamingAssets/EditData/i18n.sqlite" into and rename "_translation/i18n.sqlite.ORIGINAL" (This is your original Chinese language backup!)
     b. Run:  "python _translation/rebuild.py"
        Overwrites the game's i18n.sqlite with the merged English.
     The script auto-detects paths from its own folder. Override via "env vars TFC_SP (translation folder)" or "TFC_GAME (game root)" if you keep them elsewhere.

3. Patch the four Chinese fonts to English - AUTOMATIC
     Run:  "python _translation/font_patch.py"
     Backs up each modified .assets file as .bak on first run, then writes the English TTFs from _translation/ into the byte slots that held the Chinese fonts.
	 - Fully reversible: restore .bak.

4. (OPTIONAL) Rebuild the TextFit plugin (only if you edit the source)
     cd TextFit_plugin_source && dotnet build -c Release
     cp bin/Release/net6.0/TextFit.dll ../BepInEx/plugins/TextFit.dll

Launch the game. English text loads from i18n.sqlite; 
English fonts render inside the patched Unity assets; the TextFit plugin handles runtime UI fitting via BepInEx.

> To revert the game to Chinese: copy "i18n.sqlite.ORIGINAL" that you have as backup over "ForgottenConcluder_Data\StreamingAssets\EditData\i18n.sqlite"

## Manual re-apply (no scripts)

If you would rather not run Python at all:

Text patching (manual)
  There is no shortcut here: the i18n.sqlite format is a custom binary the game reads, not editable in a text editor. 
  You need rebuild.py (or an equivalent tool) to merge the JSON caches back into it. 
  If Python is a hard blocker, install it once from python.org, run "pip install pillow" for the wrap engine, 
  then run step 2 above,  that is the closest to "manual" available.

Font patching (manual)
  Each Chinese font is embedded inside a Unity Font asset as a byte
  slot with a 4-byte little-endian length prefix. Write the new TTF
  into that slot and zero-fill the remainder. Do NOT touch the length
  prefix and do NOT change the overall file size.

  Fonts to replace and where they live:

      resources.assets:
        FZBeiWeiKaiShu   -> Aleo-Regular.ttf         (body text)
        DFKai-SB         -> Oregano-Regular.ttf      (display)
      sharedassets0.assets:
        FZWeiBei  (x2)   -> Oregano-Regular.ttf      (display, both)
      sharedassets1.assets:
        FangSong         -> Oregano-Regular.ttf      (display)

  Procedure per font, in any hex editor with search:

    a. MAKE A BACKUP FIRST -- copy the .assets file to .assets.bak.
    b. Search for the ASCII font name (e.g. FZBeiWeiKaiShu).
    c. Near that string (usually within a few KB either direction) find the TrueType signature 00 01 00 00. 
	The nearest one whose 4-byte LE length prefix reads a value between ~10 KB and ~10 MB is the right slot.
    d. Note the length prefix value N (the slot size).
    e. Overwrite starting AT the TTF signature with the new TTF bytes.
    f. Zero-fill from (TTF signature + new TTF length) up to (TTF signature + N). File length must stay identical.
    g. Save.

  Relaunch the game -- if the font looks wrong, restore the backup and try again. 
  TMP-rendered text (quest panel body, speaker nameplate) will still show the baked kaiu atlas because TextMeshPro 1.0.55 lacks the runtime font-asset APIs
  ~~That is a hard engine limit, not a patching mistake~~

## Known issues

- Dialogue text may briefly flicker big -> small -> big (or small -> big -> small) on a new line. The TextFit plugin enforces a fixed 38pt every frame so the game's per-line content-length shrink cannot
  keep the text small; the flicker is the one frame between the game's set and ours. Trade-off: consistent readable size.
- Long English dialogue lines can clip slightly behind the speaker portrait, sually only a few letters at the end of a line.
  English is wider than the CJK the box was authored for and there is no clean way to widen the dialogue container from a plugin.
- Quest / notification panels may occasionally over-wrap short lines when the Chinese source had a semantic break.
   Usually still readable, but the wrap can look ragged.
- Tutorial pages (baked images) remain in Chinese: They are texture sprites, not runtime text. Replacing them requires per-image work and is out of scope for this patch.
- Combat and world SFX text sprites (dodge / hit / etc.) remain in Chinese for the same reason.

## Deliberately not in this zip

- Any .exe or .dll from the game / ForgottenConcluder_Data/. Steam supplies these.
- _translation/i18n.sqlite.ORIGINAL -- copy from a fresh Steam install as step 2a.
- _translation/font_backups/ -- extracted original game fonts, replicable from Steam if ever needed.
- BepInEx/LogOutput.log and the Il2CppInterop assembly cache -- both regenerate on first launch.
- Plugin build artefacts (bin/, obj/, .vs/) -- dotnet build recreates them.

## Tooling credits

The patch would not exist without these open-source projects. Please
support and star them if you build on top:

- BepInEx (bleeding-edge IL2CPP branch) -- the mod loader / plugin framework that hosts TextFit at runtime.
    https://github.com/BepInEx/BepInEx
- Il2CppInterop -- the managed-to-native bridge inside BepInEx 6 that lets a C# plugin talk to a Unity IL2CPP game.
   Patched in place to fit Unity 2018.1's older GenericMethod calling convention.
    https://github.com/BepInEx/Il2CppInterop
- unity.bepinex.dev libil2cpp source archive -- reference C++ headers used to verify method signatures while patching Il2CppInterop.
    https://unity.bepinex.dev/libil2cpp-source/
- UnityPy -- reads and writes Unity asset files from Python; used by font_patch.py to extract/inspect sprites and locate font slots.
    https://github.com/K0lb3/UnityPy
- UnityExplorer -- runtime scene / component inspector installed for BepInEx IL2CPP debugging. Not required to run the patch.
    https://github.com/sinai-dev/UnityExplorer
- UnityGameTranslator -- BepInEx runtime translation helper installed during development for reference. Not required to run the patch.
    https://github.com/djethino/UnityGameTranslator

## Font credits

Three open-source fonts from Google Fonts, all under the SIL Open Font License 1.1, are shipped in _translation/ and used by the patch:

- Aleo, by Alessio Laiso, for body text
    https://fonts.google.com/specimen/Aleo
- Oregano, by John Vargas Beltran, for display / menu titles
    https://fonts.google.com/specimen/Oregano
- Ma Shan Zheng, by Steve Matteson, for brush-styled UI sprite text
    https://fonts.google.com/specimen/Ma+Shan+Zheng
