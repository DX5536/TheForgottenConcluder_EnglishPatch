# -*- coding: utf-8 -*-
"""Separate truly hand-edited entries from auto-pass entries.

An entry counts as auto-pass if the only difference from MT is a gender
pronoun swap or one of the known phrase rewrites - i.e., something either of
my automated passes would produce.
"""
import os, re, json

SP = os.environ.get("TFC_SP", os.path.dirname(os.path.abspath(__file__)))

# Import the transforms from the quality pass so we can detect auto-produced entries
import importlib.util
spec = importlib.util.spec_from_file_location("qp", os.path.join(SP, "quality_pass.py"))
qp = importlib.util.module_from_spec(spec)

# Load caches directly rather than executing qp's write side-effects
mt = json.load(open(os.path.join(SP, "mt_cache2.json"), encoding="utf-8"))
ec = json.load(open(os.path.join(SP, "edit_cache.json"), encoding="utf-8"))
ac_path = os.path.join(SP, "auto_cache.json")
ac = json.load(open(ac_path, encoding="utf-8")) if os.path.exists(ac_path) else {}

# Re-import to skip the write; monkey-patch json.dump to a no-op
_orig_dump = json.dump
json.dump = lambda *a, **k: None
try:
    spec.loader.exec_module(qp)
finally:
    json.dump = _orig_dump

hand = {}
auto = dict(ac)
for zh, current in ec.items():
    if zh not in mt:
        # UI label / hand-authored addition (not in the MT source)
        hand[zh] = current
        continue

    mt_en = mt[zh]
    # Reproduce what the automated passes would produce from raw MT
    auto_reproduced = qp.fix_gender(mt_en)
    auto_reproduced = qp.fix_phrases(auto_reproduced)

    if current == auto_reproduced:
        # Purely automated - move to auto
        auto[zh] = current
    elif current == mt_en:
        # Unchanged from MT - not an edit at all
        pass
    else:
        # Different from both raw MT and auto-produced -> genuine hand edit
        hand[zh] = current

json.dump = _orig_dump
open(os.path.join(SP, "edit_cache.json"), "w", encoding="utf-8").write(
    json.dumps(hand, ensure_ascii=False, indent=0))
open(os.path.join(SP, "auto_cache.json"), "w", encoding="utf-8").write(
    json.dumps(auto, ensure_ascii=False, indent=0))
print(f"split: {len(hand)} hand-edited, {len(auto)} auto-pass")
