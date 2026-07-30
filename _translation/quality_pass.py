# -*- coding: utf-8 -*-
"""Comprehensive MT quality pass over the story text.

Runs three sub-passes over the MT cache and writes results to the edit cache.
Hand-edited strings are never touched.

  1. Gender:      pronouns rewritten by nearest-referent within each sentence,
                  with both a male and female roster (female mentioned = 'she').
  2. Phrasing:    common awkward MT constructions rewritten to natural English
                  ("not even one ten-thousandth of yours" -> "a mere fraction of
                  yours", "In terms of ... , I am ..." tightened, etc.).
  3. Register:    formal address for Foster Father / masters / seniors.
"""
import os, re, json

SP = os.environ.get("TFC_SP", os.path.dirname(os.path.abspath(__file__)))

MALE = [
    "Old Shen Zijun", "Shen Zijun", "Shen Wuming",
    "Foster Father", "Master Murong", "Master Juesheng",
    "Lord Ding", "Lord Shi",
    "Young Master Zhou", "Young Hero Shen", "Brother Xu",
    "Advisor Jia", "Steward Zhan", "Steward Jin",
    "Shopkeeper Feng", "Shopkeeper Wang", "Blacksmith Yan",
    "Physician Li", "Shrinekeeper Li", "Old Qian",
    "Zhang Liang", "Xiang Yu", "Ou Yezi", "Gongshu Che",
    "Zuo Xuan", "Huang Yi", "Chu Daoquan", "Dong Yu",
    "Sun Yifeng", "Fei Yizhi", "Wuzhen", "Fang Chongshan",
    "Zhu Chi", "Baili Sheng", "Zhao Siduan", "Shi Quan",
    "Liang Shu", "Lu Jun", "Yang Yunshu", "Xu Zhong",
    "Feng Lingxuan", "Shen Zhong", "Xu Jian", "Zhou Qu",
    "Qin Chenggang", "Gao Chugong", "Ying Weiren", "Chen Zhong",
    "Li Qin", "Gao Kui", "Murong Kuan", "Liu Fu",
    "Wen Bingqing", "Jian Er", "Wu Ying", "Huangfu Shuo",
    "Wei Sheng", "Zhou Zhongwei", "Yu San", "Sun Yi",
    "Jian Zhong", "Gao Feng", "Ding Wei", "Chunyu Su",
    "Hu Tiande", "Zhu Yu", "Wang Ding", "Yan Heihu",
    "Zheng Fa", "Dan Li", "Pang Chang", "Wang Ding",
    "Xiaozhuzi", "Ah-Ji",
]

FEMALE = [
    "Tong'er", "Xiao Yin", "Ling Su", "Xiaohong",
    "Ah-Juan", "Cuixiang", "Jin Sui'er", "Yan Lanqing",
    "Bai Shuangyao", "Wen Xiyue", "Lady Xue", "Lady Bai",
    "Yao Sanniang", "Yu Wu", "Madam Fang",
    "Granny Yu", "Granny Xu", "Granny Liu", "Granny Nie",
    "Sister Lan",
]

for lst in (MALE, FEMALE):
    lst.sort(key=len, reverse=True)

PRONOUNS_TO_MALE = [
    (re.compile(r"\bshe\b"),      "he"),
    (re.compile(r"\bShe\b"),      "He"),
    (re.compile(r"\bher\b"),      "his"),
    (re.compile(r"\bHer\b"),      "His"),
    (re.compile(r"\bhers\b"),     "his"),
    (re.compile(r"\bHers\b"),     "His"),
    (re.compile(r"\bherself\b"),  "himself"),
    (re.compile(r"\bHerself\b"),  "Himself"),
]
PRONOUNS_TO_FEMALE = [
    (re.compile(r"\bhe\b"),       "she"),
    (re.compile(r"\bHe\b"),       "She"),
    (re.compile(r"\bhis\b"),      "her"),
    (re.compile(r"\bHis\b"),      "Her"),
    (re.compile(r"\bhim\b"),      "her"),
    (re.compile(r"\bHim\b"),      "Her"),
    (re.compile(r"\bhimself\b"),  "herself"),
    (re.compile(r"\bHimself\b"),  "Herself"),
]

def gender_of(sentence):
    """male / female / mixed / unknown - decides how to rewrite pronouns."""
    has_male = any(m in sentence for m in MALE)
    has_female = any(f in sentence for f in FEMALE)
    if has_male and has_female: return "mixed"
    if has_male: return "male"
    if has_female: return "female"
    return "unknown"

def fix_gender(text):
    out = []
    for part in re.split(r"(?<=[.!?…])\s+", text):
        g = gender_of(part)
        if g == "male":
            for pat, rep in PRONOUNS_TO_MALE:
                part = pat.sub(rep, part)
        elif g == "female":
            for pat, rep in PRONOUNS_TO_FEMALE:
                part = pat.sub(rep, part)
        # mixed / unknown -> leave alone
        out.append(part)
    return " ".join(out) if len(out) > 1 else out[0]

# --- CJK punctuation that leaked through MT (Bing sometimes preserves the
# fullwidth forms even when it translates the surrounding text). ---
CJK_TO_ASCII = str.maketrans({
    "，": ",",   # ，
    "。": ".",   # 。
    "？": "?",   # ？
    "！": "!",   # ！
    "；": ";",   # ；
    "：": ":",   # ：
    "、": ",",   # 、
    "「": '"',   # 「
    "」": '"',   # 」
    "『": '"',   # 『
    "』": '"',   # 』
    "（": "(",   # （
    "）": ")",   # ）
})

# --- Doubled words: Chinese reduplication (门门, 慢慢, 每每) translated
# word-for-word into "gate gate", "slow slow", "each each" -- always wrong
# in English. Skip short capitalised pairs so genuine reduplicated nicknames
# like "Yun Yun" survive. ---
def dedupe_doubled_words(text):
    def repl(m):
        w = m.group(1)
        # A short capitalised pair is very likely a Chinese given-name reduplication.
        if w[0].isupper() and len(w) <= 3:
            return m.group(0)
        return w
    text = re.sub(r"\b(\w+)\s+\1\b", repl, text, flags=re.I)
    # Also catch "X or X" and "X, and X" -- MT tokenises Chinese binomes like
    # 艰深难解 (deep-and-obscure) as the same English word twice with a conjunction.
    text = re.sub(r"\b(\w+)\s+or\s+\1\b", r"\1", text, flags=re.I)
    text = re.sub(r"\b(\w+),\s+and\s+\1\b", r"\1", text, flags=re.I)
    return text

# --- Protagonist monologue: MT drops the subject and defaults to "he" when the
# Chinese source uses 我 (I) but omits the pronoun on subsequent clauses. Only
# flip he->I in the safest case: Chinese source uses 我 without 他/她, AND the
# English mentions "Foster Father" (the protagonist's specific term of address
# for his own adoptive father -- only Shen Zijun speaks about "Foster Father").
def fix_protagonist_pronouns(zh, en):
    if "我" not in zh: return en
    if "他" in zh or "她" in zh: return en
    if "Foster Father" not in en and "Foster Mother" not in en: return en
    en = re.sub(r"\bhe\b", "I", en)
    en = re.sub(r"\bHe\b", "I", en)
    en = re.sub(r"\bhis\b", "my", en)
    en = re.sub(r"\bHis\b", "My", en)
    en = re.sub(r"\bhim\b", "me", en)
    en = re.sub(r"\bHim\b", "Me", en)
    en = re.sub(r"\bhimself\b", "myself", en)
    en = re.sub(r"\bHimself\b", "Myself", en)
    return en

# --- MT run-on: Chinese frequently uses ，to separate independent clauses; MT
# translates as English "," even when a period is needed. When the following
# word is capitalised, MT itself signalled a new-sentence intent -- so we can
# usually promote the comma to a period. To avoid clobbering legit vocatives
# ("Foster Father, I'll be there") and introductory adverbials ("In cultivation,
# I am..."), the sweep is limited to two high-signal targets:
#   1. conjunctive adverbs (However, Moreover, Therefore, ...) -- ALWAYS a run-on
#      when comma-then-capital, because these connect independent clauses.
#   2. capitalised contractions (It's, I'll, That's, ...) -- almost never
#      preceded by an introductory adverbial, so a capital contraction after
#      comma is essentially always a missing period.
# For case 2 we still skip if the previous word is a known kinship / title /
# name token, keeping vocative constructions intact.

VOCATIVE_LAST_TOKENS = set("""
Father Mother Son Daughter Brother Sister Uncle Aunt Cousin
Grandfather Grandmother Granny Grandpa Grandma Nephew Niece
Master Elder Junior Senior Hero Lord Lady Sir Madam Miss Mister Mr Ms Mrs
Shopkeeper Steward Physician Blacksmith Advisor Merchant Chef Priest Priestess
Foster Big Little Old Young Second Third Fourth Fifth Sixth Seventh Eighth Ninth Tenth
Xu Sis Bro Buddy Pal Friend Comrade Companion Zijun Wuming
""".split())
try:
    _glossary_path = os.path.join(SP, "glossary.json")
    for _en_val in json.load(open(_glossary_path, encoding="utf-8")).values():
        if isinstance(_en_val, str) and _en_val:
            _tok = _en_val.split()[-1]
            if _tok and _tok[0].isupper():
                VOCATIVE_LAST_TOKENS.add(_tok)
except Exception:
    pass

_ALWAYS_FIX = ["However","Moreover","Therefore","Thus","Nevertheless","Nonetheless",
               "Meanwhile","Furthermore","Otherwise","Consequently","Instead",
               "Accordingly","Similarly","Likewise","Hence"]
_CONTRACTIONS = ["I'm","I'll","I've","I'd","We're","We'll","We've","We'd",
                 "You're","You'll","You've","You'd","He's","He'll","He'd",
                 "She's","She'll","She'd","It's","It'll","It'd",
                 "They're","They'll","They've","They'd",
                 "That's","That'll","That'd","There's","There'll",
                 "Don't","Doesn't","Didn't","Won't","Wouldn't","Can't","Couldn't",
                 "Shouldn't","Isn't","Aren't","Wasn't","Weren't","Let's"]
_ALWAYS_ALT = "|".join(re.escape(w) for w in sorted(_ALWAYS_FIX, key=len, reverse=True))
_CONTR_ALT  = "|".join(re.escape(w) for w in sorted(_CONTRACTIONS, key=len, reverse=True))
_RGX_ALWAYS = re.compile(r",(\s+)(" + _ALWAYS_ALT + r")\b")
_RGX_CONTR  = re.compile(r"(\b\w+),(\s+)(" + _CONTR_ALT + r")\b")

def fix_run_on(text):
    text = _RGX_ALWAYS.sub(lambda m: "." + m.group(1) + m.group(2), text)
    def repl(m):
        if m.group(1) in VOCATIVE_LAST_TOKENS:
            return m.group(0)
        return m.group(1) + "." + m.group(2) + m.group(3)
    return _RGX_CONTR.sub(repl, text)

# --- Leftover comma-Capital: after fix_run_on promoted the strongest cases to
# periods, what remains is comma-Capital where the capital word could plausibly
# start a sentence but the comma is grammatically correct. Reading "impact is
# too fierce, Now I feel..." trips on the capital Now -- lowercase makes it
# flow as a natural compound sentence. Never touches "I", proper nouns, or
# contractions that start with I (I'm / I'll / I've / I'd), all of which stay
# capital by convention.
_LOWERCASE_AFTER_COMMA = [
    # subject pronouns (except I)
    "It","He","She","We","They","This","That","These","Those","You",
    # modal/auxiliary verbs at clause start
    "Do","Does","Did","Can","Could","Will","Would","Should","May","Might","Must","Shall",
    "Am","Is","Are","Was","Were","Has","Have","Had","Be","Been","Being",
    # conjunctions & correlatives
    "But","And","So","Yet","Or","Nor",
    # subordinators
    "If","When","While","After","Before","Because","Although","Though","Since","Unless","As","Whether",
    # sentence-adverbs
    "Now","Then","Here","There","Also","Only","Just","Even","Perhaps","Maybe","Actually","Really","Indeed","Simply","Instead","Finally","Suddenly","Meanwhile","Certainly","Truly","Clearly","Obviously","Apparently","Naturally",
    # conjunctive adverbs kept lowercase where they legitimately follow a comma
    "However","Moreover","Therefore","Thus","Nevertheless","Nonetheless","Furthermore","Otherwise","Consequently","Accordingly","Similarly","Likewise","Hence",
    # imperative openers
    "Let","Please","Kindly","Try",
    # non-"I" contractions -- capital form after comma is almost never wanted
    "It's","It'll","It'd","That's","That'll","That'd","There's","There'll",
    "We're","We'll","We've","We'd","You're","You'll","You've","You'd",
    "He's","He'll","He'd","She's","She'll","She'd","They're","They'll","They've","They'd",
    "Don't","Doesn't","Didn't","Won't","Wouldn't","Can't","Couldn't","Shouldn't",
    "Isn't","Aren't","Wasn't","Weren't","Let's",
]
_LOWER_ALT = "|".join(re.escape(w) for w in sorted(_LOWERCASE_AFTER_COMMA, key=len, reverse=True))
_RGX_LOWER = re.compile(r"(,\s+)(" + _LOWER_ALT + r")\b")

def lowercase_after_comma(text):
    return _RGX_LOWER.sub(lambda m: m.group(1) + m.group(2)[0].lower() + m.group(2)[1:], text)

# --- Phrase rewrites ---
# Order matters: longest / most specific first.
PHRASE_REWRITES = [
    (re.compile(r"\bnot even one ten-thousandth of yours\b", re.I),
        "a mere fraction of yours"),
    (re.compile(r"\bnot even one ten-thousandth of\b", re.I),
        "a mere fraction of"),
    (re.compile(r"\bIn terms of cultivation\b"),
        "In cultivation"),
    (re.compile(r"\bIn terms of martial arts\b"),
        "In martial skill"),
    (re.compile(r"\bmy swordsmanship is still newly learned\b"),
        "my swordsmanship is still raw"),
    (re.compile(r"\bthe Foster Father\b"),
        "Foster Father"),
    # Bing sometimes tokenises the compound 义父/义母 (yìfù/yìmǔ) at a following
    # comma, producing "Foster, Father" -- the comma splits a single title.
    (re.compile(r"\bFoster,\s*Father\b"),
        "Foster Father"),
    (re.compile(r"\bFoster,\s*Mother\b"),
        "Foster Mother"),
    (re.compile(r"\bFoster,\s*Son\b"),
        "Foster Son"),
    # Doubled punctuation from run-on Chinese ("！！" "……" "？？")
    (re.compile(r"\.{4,}"),
        "..."),
    (re.compile(r"!{2,}"),
        "!"),
    (re.compile(r"\?{2,}"),
        "?"),
    (re.compile(r"!\?"),
        "?!"),
    # Missing space after sentence-final punctuation glued to next word
    (re.compile(r"([.!?])([A-Z][a-z])"),
        r"\1 \2"),
    (re.compile(r"([a-z]),([A-Z])"),
        r"\1, \2"),
    (re.compile(r"\bYes! Child\.\.\. Understood\.?"),
        "Understood, Father."),
    (re.compile(r"\bYes[!,] Understood\.?"),
        "Understood."),
    (re.compile(r"\bMaster\.\.\. Understood\.?"),
        "Understood, Master."),
    (re.compile(r"\bIt's never too early to practice facing the enemy in real combat\b"),
        "it's never too early to test them in real combat"),
    (re.compile(r"\bEven if you only learn one or two moves\b"),
        "Even a stance or two"),
    (re.compile(r"\bThose who practice martial arts value experience above all else\b"),
        "In the martial path, experience is worth more than anything"),
    (re.compile(r"\bin the martial path\b"),
        "In the martial path"),  # capitalise at sentence start
    (re.compile(r"\bmartial arts value\b"),
        "the martial arts value"),
    (re.compile(r"\bTaixu\b"),
        "Taixu"),
    (re.compile(r"\bWufang Flying Sword\b"),
        "Wufang Flying Sword"),
    # Cultivation vocabulary drift
    (re.compile(r"\btrue energy\b"),
        "true qi"),
    (re.compile(r"\bspiritual power\b"),
        "spirit force"),
    (re.compile(r"\binner force\b"),
        "inner qi"),
    (re.compile(r"\bWu Ming Valley\b"),
        "Wuming Valley"),
    # Formal register for masters
    (re.compile(r"\bFoster Father, my ([a-z]+) is (still )?newly learned\b"),
        r"Foster Father, my \1 is still raw"),
    # Common ugly constructions
    (re.compile(r"\bhaha!\b", re.I),
        "Ha!"),
    (re.compile(r"\bhahaha!\b", re.I),
        "Haha!"),
    (re.compile(r"\bhmm\.\.\.", re.I),
        "Hmm..."),
    (re.compile(r"\.\.\.\.", re.I),
        "..."),
    # Contraction awkwardness
    (re.compile(r"\bit is (very|extremely|quite)\b"),
        r"it's \1"),
    (re.compile(r"\bI am not\b"),
        "I'm not"),
    (re.compile(r"\bcannot\b"),
        "can't"),
    (re.compile(r"\bdo not\b"),
        "don't"),
    (re.compile(r"\bdid not\b"),
        "didn't"),
    (re.compile(r"\bwould not\b"),
        "wouldn't"),
    (re.compile(r"\bcould not\b"),
        "couldn't"),
    (re.compile(r"\bshould not\b"),
        "shouldn't"),
]

def fix_phrases(text):
    for pat, rep in PHRASE_REWRITES:
        text = pat.sub(rep, text)
    return text

def fix_typos(text):
    """Character-level MT artefacts: CJK punctuation, reduplication, run-ons."""
    text = text.translate(CJK_TO_ASCII)
    text = dedupe_doubled_words(text)
    text = fix_run_on(text)
    text = lowercase_after_comma(text)
    # Collapse whitespace introduced by any of the above.
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r" +\n", "\n", text)
    return text

# --- Load caches ---
# Split model:
#   edit_cache.json  = truly hand-authored (UI labels, specific tone fixes)
#   auto_cache.json  = automated passes (gender, phrase rewrites)
# Layer priority for rebuild:  edit > auto > mt
mt = json.load(open(os.path.join(SP, "mt_cache2.json"), encoding="utf-8"))
ec_path = os.path.join(SP, "edit_cache.json")
ec = json.load(open(ec_path, encoding="utf-8"))
ac_path = os.path.join(SP, "auto_cache.json")
ac = json.load(open(ac_path, encoding="utf-8")) if os.path.exists(ac_path) else {}

changed = 0
for zh, en in mt.items():
    if zh in ec:
        # Hand-authored: pass through fixes but write back to ec so tone stays
        # intact; the fixes are conservative pattern rewrites.
        new = fix_typos(ec[zh])
        new = fix_gender(new)
        new = fix_phrases(new)
        new = fix_protagonist_pronouns(zh, new)
        if new != ec[zh]:
            ec[zh] = new
            changed += 1
    else:
        new = fix_typos(en)
        new = fix_gender(new)
        new = fix_phrases(new)
        new = fix_protagonist_pronouns(zh, new)
        if new != en and new != ac.get(zh):
            ac[zh] = new
            changed += 1

json.dump(ec, open(ec_path, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
json.dump(ac, open(ac_path, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
print(f"rewrote {changed} entries; edit={len(ec)} auto={len(ac)} mt={len(mt)}")
