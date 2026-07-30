# Third-party notices

This patch bundles or depends on several open-source projects. The MIT
License in `LICENSE` covers the original code and translations in this
repository; the components listed below retain their own licenses.

## Bundled runtime dependencies

### BepInEx (bleeding-edge IL2CPP branch)
- License: LGPL-2.1-only
- Upstream: https://github.com/BepInEx/BepInEx
- Files: `BepInEx/core/*.dll`, `BepInEx/patchers/*.dll`,
  `BepInEx/interop/*.dll`, `winhttp.dll`, `doorstop_config.ini`,
  `.doorstop_version`, `changelog.txt`

### Il2CppInterop (bundled with BepInEx 6)
- License: LGPL-2.1-only
- Upstream: https://github.com/BepInEx/Il2CppInterop
- Files: `BepInEx/core/Il2CppInterop.*.dll` and generated stubs.
  A byte-level patch (documented in the plugin source header) tweaks
  two methods to fit Unity 2018.1's older GenericMethod calling
  convention. The patched DLL remains LGPL-2.1; the modifications are
  distributed under the same terms.

### .NET runtime (bundled with BepInEx)
- License: MIT
- Upstream: https://github.com/dotnet/runtime
- Files: `dotnet/*`

## Fonts

All three fonts are from Google Fonts and distributed under the SIL
Open Font License 1.1. Full license text:
https://scripts.sil.org/OFL

- Aleo, by Alessio Laiso
  - Upstream: https://fonts.google.com/specimen/Aleo
  - File: `_translation/Aleo-Regular.ttf` (and `Aleo-Bold.ttf` if
    present)
- Oregano, by John Vargas Beltrán
  - Upstream: https://fonts.google.com/specimen/Oregano
  - File: `_translation/Oregano-Regular.ttf`
- Ma Shan Zheng, by Steve Matteson
  - Upstream: https://fonts.google.com/specimen/Ma+Shan+Zheng
  - File: `_translation/MaShanZheng-Regular.ttf`

## Development-only tooling (referenced, not bundled)

- UnityPy -- MIT -- https://github.com/K0lb3/UnityPy
- UnityExplorer -- MIT -- https://github.com/sinai-dev/UnityExplorer
- UnityGameTranslator -- see upstream -- https://github.com/djethino/UnityGameTranslator

## Game

*The Forgotten Concluder* (*霸剑霄云录*) is © Tome Creatives and NOT
covered by any license in this repository. No game code, assets, text,
or fonts are distributed here. Users must own a legitimate copy of the
game to use this patch.

    https://store.steampowered.com/app/2159730/The_Forgotten_Concluder/
