# -*- coding: utf-8 -*-
"""Fix machine-translation gender errors around known male characters.

MT often defaults Chinese third-person singular (which is oral-neutral) to "she"
when the referent is unmarked. For characters we know are male, run through the
MT cache and rewrite same-sentence female pronouns to male.

Applied to a separate cache so re-runs are stable and reversible.
"""
import os, json, re

SP = os.environ.get("TFC_SP", os.path.dirname(os.path.abspath(__file__)))

# Known male referents. Order matters slightly (longer names first) but every
# entry is a hard male, so mistakes will only ever mis-gender to male, not the
# other way around.
MALE = [
    "Foster Father", "Shen Zijun", "Old Shen Zijun", "Shen Wuming",
    "Master Murong", "Master Juesheng",
    "Lord Ding", "Lord Shi",
    "Young Master Zhou", "Young Hero Shen",
    "Brother Xu", "Xu Zhong",
    "Old Qian", "Blacksmith Yan", "Physician Li", "Shrinekeeper Li",
    "Advisor Jia", "Steward Zhan", "Steward Jin", "Shopkeeper Feng", "Shopkeeper Wang",
    "Zhang Liang", "Xiang Yu",
    "Ou Yezi", "Gongshu Che", "Zuo Xuan", "Huang Yi",
    "Chu Daoquan", "Dong Yu", "Sun Yifeng", "Fei Yizhi", "Wuzhen",
    "Fang Chongshan", "Zhu Chi",
    "Baili Sheng", "Zhao Siduan", "Shi Quan", "Liang Shu", "Lu Jun",
    "Yang Yunshu", "Xu Zhong", "Feng Lingxuan", "Shen Zhong", "Xu Jian",
    "Zhou Qu", "Qin Chenggang", "Gao Chugong", "Ying Weiren", "Chen Zhong",
    "Li Qin", "Gao Kui", "Murong Kuan", "Liu Fu", "Wen Bingqing",
    "Jian Er", "Wu Ying", "Huangfu Shuo", "Wei Sheng", "Zhou Zhongwei",
    "Yu San", "Sun Yi", "Jian Zhong", "Gao Feng", "Ding Wei",
    "Chunyu Su", "Hu Tiande", "Zhu Yu", "Wang Ding",
    "Yan Heihu", "Zheng Fa", "Dan Li", "Pang Chang",
]
MALE.sort(key=len, reverse=True)

# Pronouns/possessives to rewrite. Case-sensitive so proper-noun "She"/"Her"
# at sentence start still gets a capitalised replacement.
PRONOUNS = [
    (r"\bshe\b", "he"),
    (r"\bShe\b", "He"),
    (r"\bher\b", "his"),          # simplifying assumption: attributive is more common
    (r"\bHer\b", "His"),
    (r"\bhers\b", "his"),
    (r"\bHers\b", "His"),
    (r"\bherself\b", "himself"),
    (r"\bHerself\b", "Himself"),
]

def has_male_referent(sentence):
    return any(m in sentence for m in MALE)

def rewrite(text):
    # Work sentence-by-sentence: rewriting only where a male name appears in the
    # same sentence avoids clobbering female characters who might share the line.
    out = []
    for part in re.split(r"(?<=[.!?])\s+", text):
        if has_male_referent(part):
            for pat, rep in PRONOUNS:
                part = re.sub(pat, rep, part)
        out.append(part)
    return " ".join(out) if len(out) > 1 else out[0]

mt = json.load(open(os.path.join(SP, "mt_cache2.json"), encoding="utf-8"))
ec_path = os.path.join(SP, "edit_cache.json")
ec = json.load(open(ec_path, encoding="utf-8"))

changed = 0
for zh, en in mt.items():
    if zh in ec:
        continue                     # hand-edited strings are the source of truth
    new = rewrite(en)
    if new != en:
        ec[zh] = new
        changed += 1

# also revisit already-edited strings (some might have inherited MT phrasing)
for zh, en in list(ec.items()):
    if any(m in en for m in MALE):
        new = rewrite(en)
        if new != en:
            ec[zh] = new
            changed += 1

json.dump(ec, open(ec_path, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
print(f"rewrote {changed} entries; edit_cache now {len(ec)}")
