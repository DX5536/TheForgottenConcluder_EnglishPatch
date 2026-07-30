import struct, json, os, re
from PIL import ImageFont
# SP = folder that holds the caches and fonts. Defaults to this script's own
# directory (i.e. `_translation/`). Override with the TFC_SP env var if you
# keep them elsewhere.
SP = os.environ.get("TFC_SP", os.path.dirname(os.path.abspath(__file__)))
# GAME_ROOT defaults to the parent of SP (the game folder that contains
# ForgottenConcluder_Data). Override with TFC_GAME env var if different.
GAME_ROOT = os.environ.get("TFC_GAME", os.path.dirname(SP))
SRC = os.path.join(SP, "i18n.sqlite.ORIGINAL")
DST = os.path.join(GAME_ROOT, "ForgottenConcluder_Data", "StreamingAssets", "EditData", "i18n.sqlite")

PERSON_NAMES = ["Old Shen Zijun","Shen Zijun","Shen Wuming","Xiaohong","Tong'er","Ah-Juan","Ah-Ji",
"Xiao Yin","Granny Yu","Granny Xu","Granny Liu","Granny Nie","Pang Chang","Dan Li","Han Linsha",
"Zhang Liang","Xiang Yu","Bai Shuangyao","Ling Su","Xiaozhuzi","Huang Yi","Zuo Xuan","Yan Lanqing",
"He Shangzhi","Gongshu Che","Qi Yanjun","Qi Wujiang","Blacksmith Yan","Madam Fang","Cuixiang",
"Wuzhen","Wang Ding","Yan Heihu","Zheng Fa","Gao Feng","Ding Wei","Chunyu Su","Hu Tiande",
"Advisor Jia","Zhu Yu","Mei Xiu","Yu Wu","Steward Zhan","Steward Jin","Physician Li","Baili Sheng",
"Old Qian","Chu Daoquan","Dong Yu","Sun Yifeng","Master Juesheng","Shrinekeeper Li","Fei Yizhi",
"Ou Yezi","Zhao Siduan","Lady Xue","Lady Bai","Wen Xiyue","Shi Quan","Liang Shu","Lu Jun",
"Shopkeeper Feng","Yang Yunshu","Jin Sui'er","Xu Zhong","Feng Lingxuan","Shen Zhong","Xu Jian",
"Zhou Qu","Qin Chenggang","Gao Chugong","Ying Weiren","Chen Zhong","Li Qin","Gao Kui","Murong Kuan",
"Liu Fu","Wen Bingqing","Jian Er","Yao Sanniang","Wu Ying","Huangfu Shuo","Wei Sheng","Zhou Zhongwei",
"Yu San","Sun Yi","Jian Zhong","Fang Chongshan","Lü Erniang","Zhu Chi","Foster Father","Sister Lan",
"Master Murong","Brother Xu","Lord Ding","Lord Shi","Shopkeeper Wang","Young Hero Shen","Young Master Zhou"]
PERSON_NAMES.sort(key=len, reverse=True)

def embolden(en):
    # dialogue/narration only: long enough and sentence-like
    if len(en) < 20 or not any(c in en for c in ".!?,"): return en
    toks = {}
    for i, nm in enumerate(PERSON_NAMES):
        if nm in en:
            tok = f"\ue000{i}\ue001"
            en = re.sub(r"(?<![A-Za-z])" + re.escape(nm) + r"(?![A-Za-z])", tok, en)
            toks[tok] = nm
    for tok, nm in toks.items():
        en = en.replace(tok, f"<b>{nm}</b>")
    return en


EM = 100
BUDGET_EM = 25.5                      # dialogue box width, from p90 of authored Chinese lines
AUTHORED_MAX_EM = 34                  # only re-wrap strings authored to the dialogue box
font = ImageFont.truetype(os.path.join(SP, "Aleo-Regular.ttf"), EM)
def width(s): return font.getlength(s)

def zh_em(s):
    return sum(1.0 if ord(c) > 0x2E80 else 0.5 for c in s)

MIN_TAIL_CHARS = 10   # a trailing fragment shorter than this gets folded up

# Split a run of prose into sentences, keeping the terminator with its sentence.
_SENT_END = re.compile(r'(?<=[.!?…])\s+')

def sentences(text):
    parts = _SENT_END.split(text)
    return [p.strip() for p in parts if p.strip()]

def wrap(line, budget_px):
    """Greedy sentence-packed wrap.

    Rule: fit as many whole sentences into each line as the budget allows; never
    break a sentence mid-clause unless it alone exceeds the budget. A sentence
    boundary is always where a new line starts, so lines break "cleanly" rather
    than at whatever random word the width happens to land on.

    "Yes! Child... Understood." (two short sentences) -> one line.
    Long single-sentence lines still word-wrap inside themselves as a fallback.
    """
    sents = sentences(line)
    if not sents:
        return [line]

    out, cur = [], ""
    for s in sents:
        if not cur:
            # A single sentence may still overflow - word-wrap it in place.
            if width(s) <= budget_px:
                cur = s
            else:
                parts = []
                buf = ""
                for word in s.split(" "):
                    trial = word if not buf else buf + " " + word
                    if width(trial) <= budget_px:
                        buf = trial
                    else:
                        if buf: parts.append(buf)
                        buf = word
                if buf: parts.append(buf)
                # first parts flush; keep the tail as `cur` so a following short
                # sentence can still share it
                out.extend(parts[:-1])
                cur = parts[-1] if parts else s
        else:
            trial = cur + " " + s
            if width(trial) <= budget_px:
                cur = trial
            else:
                out.append(cur)
                cur = s
                # long sentence starting fresh line may still overflow
                if width(cur) > budget_px:
                    parts = []
                    buf = ""
                    for word in cur.split(" "):
                        t2 = word if not buf else buf + " " + word
                        if width(t2) <= budget_px:
                            buf = t2
                        else:
                            if buf: parts.append(buf)
                            buf = word
                    if buf: parts.append(buf)
                    out.extend(parts[:-1])
                    cur = parts[-1] if parts else s
    if cur:
        out.append(cur)

    # Fold short tails upward (e.g. leftover "yours,")
    i = 0
    while i < len(out):
        if len(out) > 1 and len(out[i]) < MIN_TAIL_CHARS:
            if i == 0:
                out[0] = out[0] + " " + out[1]
                del out[1]
            else:
                out[i-1] = out[i-1] + " " + out[i]
                del out[i]
                i -= 1
                continue
        i += 1
    return out

def read7(b, off):
    v = 0; s = 0
    while True:
        c = b[off]; off += 1; v |= (c & 0x7F) << s
        if not (c & 0x80): return v, off
        s += 7
def write7(n):
    o = bytearray()
    while True:
        c = n & 0x7F; n >>= 7
        if n: o.append(c | 0x80)
        else: o.append(c); return bytes(o)

layers = []
for name in ("edit_cache.json", "auto_cache.json", "mt_cache2.json"):
    p = os.path.join(SP, name)
    layers.append(json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {})
def lookup(zh):
    for L in layers:
        if zh in L and L[zh]: return L[zh]
    return None

d = open(SRC, "rb").read()
count = struct.unpack_from("<I", d, 0)[0]; off = 4; recs = []
for i in range(count):
    ident = struct.unpack_from("<I", d, off)[0]; off += 4
    sl = []
    for s in range(10):
        ln, off = read7(d, off); sl.append(d[off:off+ln].decode("utf-8")); off += ln
    recs.append([ident, sl])

hit = rewrapped = 0
budget_px = BUDGET_EM * EM
for ident, sl in recs:
    zh = sl[1]
    if not zh: continue
    en = lookup(zh)
    if not en: continue
    hit += 1
    en = embolden(en)
    zh_lines = zh.replace("\r\n", "\n").split("\n")
    max_em = max(zh_em(l) for l in zh_lines)
    # Only re-wrap when the Chinese was authored to FILL the dialogue box.
    # Heuristic:
    #   1. At least one line is close to full box width (>=22 em, <=34 em), AND
    #   2. NOT a multi-line narrative -- i.e. no explicit sentence terminators
    #      inside individual lines. Loading blurbs and quest descriptions have
    #      lines ending with 。！？, dialogue lines typically don't (they use
    #      the trailing arrow-advance instead of an in-line full stop).
    ends_with_sentence_terminator = any(
        l.rstrip().endswith(("。", "！", "？", "!", "?", "."))
        for l in zh_lines
    )
    is_multiline_narrative = len(zh_lines) > 1 and ends_with_sentence_terminator
    is_dialogue_box = (max_em >= 22 and max_em <= AUTHORED_MAX_EM
                       and not is_multiline_narrative)
    if is_dialogue_box:
        sep = "\r\n" if "\r\n" in en else "\n"
        # Flatten authored line breaks first: the source-language breaks land
        # mid-sentence in English. Then wrap() re-breaks on sentence boundaries.
        flat = en.replace("\r\n", " ").replace("\n", " ")
        while "  " in flat:
            flat = flat.replace("  ", " ")
        new_lines = wrap(flat.strip(), budget_px)
        if [flat.strip()] != new_lines or "\n" in en:
            rewrapped += 1
        en = sep.join(new_lines)
    else:
        # Non-dialogue string (loading blurb, quest description, tooltip, ...).
        # The English cache may still carry \n baked in from a previous run's
        # dialogue-box re-wrap. Flatten it, then re-split ONCE per Chinese \n
        # so the sentence structure matches the source (one Chinese sentence
        # = one English line).
        zh_segments = [s for s in zh.replace("\r\n", "\n").split("\n") if s.strip()]
        if len(zh_segments) > 1:
            # Flatten English -> single line
            flat = en.replace("\r\n", " ").replace("\n", " ")
            while "  " in flat: flat = flat.replace("  ", " ")
            flat = flat.strip()
            # Split English into sentences using terminal punctuation.
            en_sents = re.split(r"(?<=[.!?…])\s+", flat)
            # If English has as many sentences as Chinese has segments, pair
            # them 1:1 and rejoin with \n; otherwise leave flat (safer than
            # a mid-clause guess).
            if len(en_sents) == len(zh_segments):
                # Where a Chinese segment ends with ， (comma + line break in
                # source), the analogous English boundary should be a period,
                # not the comma MT emitted. Detect and fix.
                out_sents = []
                for i, s in enumerate(en_sents):
                    zh_seg = zh_segments[i].strip()
                    if zh_seg.endswith("，") or zh_seg.endswith(",") or zh_seg.endswith("、"):
                        if i < len(en_sents) - 1 and s.rstrip().endswith(","):
                            s = s.rstrip()[:-1] + "."
                    out_sents.append(s)
                en = "\n".join(out_sents)
            else:
                # Sentence counts don't match -- typically because MT joined two
                # Chinese sentences with a comma into one English sentence. Split
                # proportionally: for each Chinese \n boundary, find the analogous
                # comma in the English and promote it to a period + newline.
                total_zh = sum(len(s) for s in zh_segments)
                cursor = 0
                pieces = []
                for i, zh_seg in enumerate(zh_segments[:-1]):
                    cursor += len(zh_seg)
                    target_en = int(len(flat) * cursor / total_zh) if total_zh else 0
                    # Search for a comma within +/- 20 chars of the proportional
                    # position; the nearest one is the semantic boundary.
                    window = 25
                    best = -1
                    for j in range(max(0, target_en - window),
                                   min(len(flat), target_en + window)):
                        if flat[j] == ",":
                            if best < 0 or abs(j - target_en) < abs(best - target_en):
                                best = j
                    if best > 0 and best not in [p[0] for p in pieces]:
                        pieces.append((best, i))
                if pieces:
                    parts = []
                    prev = 0
                    for pos, _ in pieces:
                        parts.append(flat[prev:pos].rstrip() + ".")
                        prev = pos + 1
                    parts.append(flat[prev:].lstrip())
                    en = "\n".join(parts)
                else:
                    en = flat
    sl[1] = en
    sl[2] = en

out = bytearray(struct.pack("<I", count))
for ident, sl in recs:
    out += struct.pack("<I", ident)
    for v in sl:
        e = v.encode("utf-8"); out += write7(len(e)) + e
open(DST, "wb").write(bytes(out))
print(f"rebuilt: {hit} translated, {rewrapped} re-wrapped to {BUDGET_EM} em, size {len(out):,}")
