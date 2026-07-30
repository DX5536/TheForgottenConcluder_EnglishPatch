Translation project for The Forgotten Concluder (霸剑霄云录)
============================================================
The game's entire text lives in ForgottenConcluder_Data\StreamingAssets\EditData\i18n.sqlite
(custom binary, NOT sqlite):
  [uint32 record_count]
  per record: [id:u32 LE] then 10 strings, each [7-bit-encoded length][UTF-8 bytes]
  Slots: 0=Traditional (lookup key), 1=Simplified (DISPLAYED), 2=English, 3=JP, 4=KR, 5-9 unused

Files:
  i18n.sqlite.ORIGINAL  - untouched original file (restore = copy over the live one)
  glossary.json         - zh->en names/terms, substituted BEFORE machine translation
  mt_cache2.json        - Bing machine translation of all 14,721 unique strings
  edit_cache.json       - hand-polished strings (wuxia tone); wins over mt_cache2
  rebuild.py            - rebuilds the live i18n.sqlite from ORIGINAL + caches
                          (edit paths inside if scratchpad moved; layer order: edit > mt > zh)

To revert the game to Chinese: copy i18n.sqlite.ORIGINAL over
ForgottenConcluder_Data\StreamingAssets\EditData\i18n.sqlite
