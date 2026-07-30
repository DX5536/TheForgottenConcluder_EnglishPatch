"""Font byte-patcher for The Forgotten Concluder.

Replaces the four Chinese fonts embedded in the game's Unity asset files
with English TTFs, in place. Preserves every other byte of the asset file
so the game's Unity serialiser still accepts it -- writes the new TTF into
the old font's byte slot and zero-fills the remainder; the 4-byte length
prefix that Unity reads is untouched.

Usage:
    python font_patch.py                    # runs against this game folder
    python font_patch.py <game_root>        # runs against a different folder

Creates a .bak alongside each patched .assets file the first time it runs
so re-patching is safe. Delete the .bak to force a fresh backup.
"""
import os, sys, struct
from pathlib import Path

# Fonts to replace. (asset file, Font asset name in the file, replacement TTF).
# Names come from Unity Font m_Name. TTFs live in _translation/.
# The same asset name can appear more than once inside the same file (Oregano
# replaces two FZWeiBei fonts in sharedassets0); each entry is one occurrence.
PATCHES = [
    ("ForgottenConcluder_Data/resources.assets",     "FZBeiWeiKaiShu", "_translation/Aleo-Regular.ttf"),
    ("ForgottenConcluder_Data/sharedassets0.assets", "FZWeiBei",       "_translation/Oregano-Regular.ttf"),
    ("ForgottenConcluder_Data/sharedassets0.assets", "FZWeiBei",       "_translation/Oregano-Regular.ttf"),
    ("ForgottenConcluder_Data/resources.assets",     "DFKai-SB",       "_translation/Oregano-Regular.ttf"),
    ("ForgottenConcluder_Data/sharedassets1.assets", "FangSong",       "_translation/Oregano-Regular.ttf"),
]

# TrueType outline magic. OpenType-with-TT outlines uses the same signature.
TTF_SIG = b"\x00\x01\x00\x00"
# Reasonable font slot bounds so a wrong anchor can't corrupt the file.
MIN_SLOT_BYTES = 8 * 1024
MAX_SLOT_BYTES = 20 * 1024 * 1024

def find_nth(data: bytes, needle: bytes, n: int) -> int:
    """Return the byte offset of the nth (1-indexed) occurrence, or -1."""
    pos = -1
    for _ in range(n):
        pos = data.find(needle, pos + 1)
        if pos < 0:
            return -1
    return pos

def patch_one(data: bytearray, font_name: str, occurrence: int, new_ttf: bytes, asset_rel: str) -> None:
    name_bytes = font_name.encode("ascii")
    name_pos = find_nth(bytes(data), name_bytes, occurrence)
    if name_pos < 0:
        raise RuntimeError(f"font name '{font_name}' occurrence {occurrence} not found in {asset_rel}")

    # A Font asset stores m_FontData as a length-prefixed byte array. The TTF
    # data can be laid out before OR after the m_Name string depending on the
    # Unity serialiser version. Scan both directions from the name and pick
    # the TTF signature closest to it whose length prefix looks sane.
    search_lo = max(0, name_pos - 20 * 1024 * 1024)
    search_hi = min(len(data), name_pos + 20 * 1024 * 1024)
    region = bytes(data[search_lo:search_hi])

    best = None      # (distance-from-name, ttf_offset_in_data, slot_length)
    off = 0
    while True:
        idx = region.find(TTF_SIG, off)
        if idx < 0:
            break
        off = idx + 4
        absolute = search_lo + idx
        # Length prefix immediately precedes the TTF bytes (4-byte LE uint).
        if absolute < 4:
            continue
        slot_len = struct.unpack_from("<I", data, absolute - 4)[0]
        if slot_len < MIN_SLOT_BYTES or slot_len > MAX_SLOT_BYTES:
            continue
        # The length must actually fit in the file from this offset.
        if absolute + slot_len > len(data):
            continue
        dist = abs(absolute - name_pos)
        if best is None or dist < best[0]:
            best = (dist, absolute, slot_len)

    if best is None:
        raise RuntimeError(
            f"no plausible TTF slot found near '{font_name}' occurrence {occurrence} in {asset_rel}")
    _, ttf_start, slot_len = best

    if len(new_ttf) > slot_len:
        raise RuntimeError(
            f"replacement TTF ({len(new_ttf)} B) doesn't fit slot ({slot_len} B) for '{font_name}'. "
            f"Use a smaller font or subset it.")

    # Write new TTF, zero-fill remainder of the slot. Length prefix unchanged.
    data[ttf_start : ttf_start + len(new_ttf)] = new_ttf
    for i in range(ttf_start + len(new_ttf), ttf_start + slot_len):
        data[i] = 0

    print(f"  patched '{font_name}' #{occurrence} @ 0x{ttf_start:X} "
          f"(slot {slot_len} B -> new {len(new_ttf)} B) in {asset_rel}")

def main():
    game_root = Path(sys.argv[1] if len(sys.argv) > 1 else
                     os.path.dirname(os.path.abspath(__file__)) + "/..").resolve()
    print(f"game root: {game_root}")

    # Group patches by asset file so each file is loaded once.
    per_file: dict[str, list[tuple[str, int, str]]] = {}
    seen: dict[tuple[str, str], int] = {}
    for asset_rel, font_name, ttf_rel in PATCHES:
        occ = seen.get((asset_rel, font_name), 0) + 1
        seen[(asset_rel, font_name)] = occ
        per_file.setdefault(asset_rel, []).append((font_name, occ, ttf_rel))

    for asset_rel, patches in per_file.items():
        asset_path = game_root / asset_rel
        if not asset_path.exists():
            print(f"  MISSING asset: {asset_path}"); continue

        # Back up once per file so re-runs stay reversible.
        bak = asset_path.with_suffix(asset_path.suffix + ".bak")
        if not bak.exists():
            bak.write_bytes(asset_path.read_bytes())
            print(f"  wrote backup: {bak.name}")

        data = bytearray(asset_path.read_bytes())
        for font_name, occ, ttf_rel in patches:
            ttf_path = game_root / ttf_rel
            if not ttf_path.exists():
                print(f"  MISSING TTF: {ttf_path}"); continue
            new_ttf = ttf_path.read_bytes()
            patch_one(data, font_name, occ, new_ttf, asset_rel)
        asset_path.write_bytes(bytes(data))
        print(f"  wrote {asset_rel} ({len(data):,} B)")

if __name__ == "__main__":
    main()
