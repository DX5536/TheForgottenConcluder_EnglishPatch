using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TextFit
{
    // Works on Unity 2018.1 IL2CPP only because Il2CppInterop.Runtime.dll has been patched:
    //   * GenericMethod_GetMethod_Hook.FindTargetMethod -> IntPtr.Zero
    //       (its byte-signature scan and 2-arg detour are invalid for 2018.1, where
    //        GenericMethod::GetMethod takes ONE argument -- verified against libil2cpp source)
    //   * Hook<T>.TargetMethodNotFound -> no-op, so a missing hook is skipped, not fatal
    // Plus Il2Cppmscorlib.dll stubs for GC.ReRegisterForFinalize and Type.op_Equality.
    [BepInPlugin("dev.textfit.plugin", "TextFit", "1.0.0")]
    public class TextFitPlugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        internal static bool DoAutoSize = true;
        internal static bool DoRichText = true;
        internal static float MinRatio = 0.55f;
        internal static float Rescan = 1.0f;
        internal static string BodyFontName = "";
        internal static string BodyObjects = "";
        internal static float BodyFontScale = 1.0f;
        internal static string OSFontName = "";
        internal static string TitleFontName = "";
        internal static string TitleObjects = "";
        internal static bool NoWrapShortLabels = true;
        internal static bool LogSprites = true;
        internal static string SpriteDir = "";
        internal static int MinLabelSize = 14;
        internal static int MaxLabelChars = 25;
        internal static float MaxShrink = 0.55f;
        internal static string NoShrinkObjects = "";
        internal static string WidenObjects = "";
        internal static string SentenceBreakObjects = "";
        internal static bool SideAwareWrap = true;
        internal static int LeftSpeakerLineWidth = 46;
        internal static int RightSpeakerLineWidth = 30;
        internal static string SpeakerTextObject = "TalkText";
        internal static string SpeakerNameObject = "Name";
        // Real dialogue body: a UI Text whose immediate parent is named "Balabala"
        // (the intra-Talk_UI container). "TalkText" we historically targeted is a
        // hidden legacy dialogue system, which is why the fixes never took visible effect.
        internal static string DialogueParentName = "Balabala";
        internal static float SpriteRescan = 0.1f;
        internal static bool FitBodyHeight = true;
        internal static int DialogueFontSize = 38;
        internal static string SubItemParents = "Objective,TargetWrapper";
        internal static float SubItemBodyScale = 0.55f;

        public override void Load()
        {
            Logger = Log;
            DoAutoSize = Config.Bind("General", "EnableAutoSize", true,
                "Shrink text automatically so it fits inside its box.").Value;
            DoRichText = Config.Bind("General", "EnableRichText", true,
                "Enable rich text tags (<b>, <color>, ...) on all text components.").Value;
            MinRatio = Config.Bind("General", "MinSizeRatio", 0.55f,
                "Smallest allowed size as a fraction of the original font size.").Value;
            Rescan = Config.Bind("General", "RescanSeconds", 1.0f,
                "How often to scan for newly created text objects.").Value;

            // Font reassignment. The game uses one font asset for both the speaker name and
            // the dialogue body, so only a per-component swap can separate them.
            // Names live in config because they are non-ASCII (the original CJK asset names).
            BodyFontName = Config.Bind("Fonts", "BodyFontName", "",
                "Name of the font asset to use for body text (dialogue, descriptions). Empty = leave alone.").Value;
            BodyObjects = Config.Bind("Fonts", "BodyTextObjects",
                "TalkText,Description_Text,DetailImageText,Content,Target",
                "Comma-separated GameObject names that should use the body font.").Value;
            BodyFontScale = Config.Bind("Fonts", "BodyFontScale", 0.85f,
                "Size correction for body text. Aleo renders larger than the CJK font it replaces at the same point size; 1.0 disables.").Value;
            SpriteDir = Config.Bind("Sprites", "SpriteDir",
                System.IO.Path.Combine(Paths.PluginPath, "TextFit_Sprites"),
                "Folder of PNG replacements. Each file is named after the sprite it replaces.").Value;
            LogSprites = Config.Bind("General", "LogSprites", true,
                "Log every UI Image and its sprite name, so brush-art labels can be identified for replacement.").Value;
            MinLabelSize = Config.Bind("General", "MinLabelSize", 14,
                "Smallest point size a shrunk-to-fit label may reach.").Value;
            MaxLabelChars = Config.Bind("General", "MaxLabelChars", 25,
                "Only text this short counts as a label. Longer text is left to wrap instead of being shrunk.").Value;
            MaxShrink = Config.Bind("General", "MaxShrink", 0.55f,
                "Hardest a label may be shrunk, as a fraction of its original size.").Value;
            NoShrinkObjects = Config.Bind("General", "NoShrinkObjects", "",
                "Comma-separated GameObject names that must keep their full size. Supports Name@170 (width-qualified) and Setting* (prefix).").Value;
            SentenceBreakObjects = Config.Bind("General", "SentenceBreakObjects", "DetailImageText",
                "Comma-separated GameObject names whose text should break only after a sentence ends.").Value;
            SideAwareWrap = Config.Bind("Dialogue", "SideAwareWrap", true,
                "Rewrap dialogue based on where the nameplate is: left speaker gets wider lines (portrait doesn't block the right side); right speaker gets narrower lines (avoid running under the portrait).").Value;
            LeftSpeakerLineWidth = Config.Bind("Dialogue", "LeftSpeakerLineWidth", 46,
                "Max characters per line when the speaker's nameplate is on the LEFT.").Value;
            RightSpeakerLineWidth = Config.Bind("Dialogue", "RightSpeakerLineWidth", 30,
                "Max characters per line when the speaker's nameplate is on the RIGHT (avoid overlapping the portrait).").Value;
            SpeakerTextObject = Config.Bind("Dialogue", "SpeakerTextObject", "TalkText",
                "GameObject name of the dialogue text component.").Value;
            SpeakerNameObject = Config.Bind("Dialogue", "SpeakerNameObject", "Name",
                "GameObject name of the nameplate. Used to determine left/right speaker.").Value;
            DialogueParentName = Config.Bind("Dialogue", "DialogueParentName", "Balabala",
                "Immediate parent name of the visible dialogue Text component. Confirmed via runtime hierarchy dump: dialogue body lives at Talk_UI/Talk/Background/Balabala/Text.").Value;
            WidenObjects = Config.Bind("General", "WidenObjects", "",
                "Comma-separated Name:pixels entries. Widens a component's rect so long text fits at full size instead of shrinking or clipping.").Value;
            SpriteRescan = Config.Bind("General", "SpriteRescanSeconds", 0.1f,
                "How often to run the sprite-swap pass. Lower = selected states change faster.").Value;
            FitBodyHeight = Config.Bind("General", "FitBodyHeight", true,
                "Shrink body text that overflows its box vertically, so it stops colliding with elements above.").Value;
            DialogueFontSize = Config.Bind("Dialogue", "DialogueFontSize", 38,
                "Fixed point size for dialogue text (TalkText). Enforced every scan so the game cannot resize it. Derived from 45pt Chinese authored size * BodyFontScale.").Value;
            SubItemParents = Config.Bind("General", "SubItemParents", "Objective",
                "Comma-separated parent GameObject names. A body-text child of one of these is treated as a sub-item and scaled down (see SubItemBodyScale). Lets the quest-objective list render smaller than the quest description above it.").Value;
            SubItemBodyScale = Config.Bind("General", "SubItemBodyScale", 0.55f,
                "Extra scale multiplier applied to body-text children of a SubItemParents parent. 0.55 = roughly half the size of the surrounding brown text.").Value;
            NoWrapShortLabels = Config.Bind("General", "NoWrapShortLabels", true,
                "Let short labels in narrow boxes shrink onto one line instead of stacking into a column.").Value;
            TitleFontName = Config.Bind("Fonts", "TitleFontName", "",
                "Font asset to use for headings and names (the display face). Empty = leave alone.").Value;
            TitleObjects = Config.Bind("Fonts", "TitleTextObjects", "NameText",
                "Comma-separated GameObject names that should use the title font.").Value;
            OSFontName = Config.Bind("Fonts", "OSFontName", "Aleo",
                "Windows-installed font FAMILY to use for body text. A family gives Unity a real Bold face, so <b> stops being faked. Empty = use the embedded asset instead.").Value;

            try
            {
                AddComponent<TextFitBehaviour>();
                Log.LogInfo("TextFit loaded - behaviour injected.");
            }
            catch (Exception e)
            {
                Log.LogError("AddComponent failed: " + e);
            }
        }
    }

    public class TextFitBehaviour : MonoBehaviour
    {
        public TextFitBehaviour(IntPtr ptr) : base(ptr) { }

        private bool _parsedBodyObjects;
        private bool _loggedSide;
        private bool _loggedSideMiss;
        private string _lastTalkText;
        private int _lastTalkSide;
        private float _timer;
        private int _total;
        private int _failures;
        private bool _loggedFirstScan;
        private readonly System.Collections.Generic.HashSet<string> _seen =
            new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.Dictionary<string, Font> _fonts =
            new System.Collections.Generic.Dictionary<string, Font>();
        private readonly System.Collections.Generic.HashSet<int> _scaled =
            new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.Dictionary<int, int> _origSize =
            new System.Collections.Generic.Dictionary<int, int>();
        // Per-GameObject-name max size ever seen. Survives instance recreation (e.g.,
        // after a battle scene reload) — without this, TalkText's post-reload instance
        // could be captured at a game-shrunk value and lock in permanently small.
        private readonly System.Collections.Generic.Dictionary<string, int> _origSizeByName =
            new System.Collections.Generic.Dictionary<string, int>();
        private readonly System.Collections.Generic.Dictionary<int, string> _lastText =
            new System.Collections.Generic.Dictionary<int, string>();
        private readonly System.Collections.Generic.Dictionary<string, float> _widen =
            new System.Collections.Generic.Dictionary<string, float>();
        private readonly System.Collections.Generic.HashSet<int> _widened =
            new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.HashSet<string> _widenLeft =
            new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<string> _sentenceBreak =
            new System.Collections.Generic.HashSet<string>(
                TextFitPlugin.SentenceBreakObjects.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
        private float _spriteTimer;
        private readonly System.Collections.Generic.HashSet<string> _noShrink =
            new System.Collections.Generic.HashSet<string>(
                TextFitPlugin.NoShrinkObjects.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
        private readonly System.Collections.Generic.HashSet<string> _titleSet =
            new System.Collections.Generic.HashSet<string>(
                TextFitPlugin.TitleObjects.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
        private readonly System.Collections.Generic.HashSet<string> _subItemParents =
            new System.Collections.Generic.HashSet<string>(
                TextFitPlugin.SubItemParents.Split(new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
        // Dialogue fast-path runs every frame (no timer) so the enforcement wins the
        // race against the game's per-line font resize -- eliminates the visible flicker.
        // "TalkText,Content:0.7" -> set of names, plus optional per-object scale overrides
        private readonly System.Collections.Generic.HashSet<string> _bodySet =
            new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.Dictionary<string, float> _bodyScale =
            new System.Collections.Generic.Dictionary<string, float>();

        // Detect speaker side. The dialogue system has two avatar branches under the
        // nameplate: "LeftAvatar" (Shen Zijun etc.) and "RightAvatar" (NPCs). Only one
        // is active at a time and its name is literally the side. Walk up from TalkText
        // to find whichever branch is currently active.
        //   returns -1 = left, +1 = right, 0 = unknown
        private int DetectSpeakerSide(Text talk)
        {
            try
            {
                // Walk up the parent chain; from each level, search a couple of levels
                // deep for a "LeftAvatar" or "RightAvatar" that is active in hierarchy.
                var t = talk.transform;
                for (int up = 0; up < 5 && t != null; up++)
                {
                    int found = SearchAvatars(t, 3);
                    if (found != 0)
                    {
                        if (!_loggedSide)
                        {
                            _loggedSide = true;
                            TextFitPlugin.Logger.LogInfo(
                                "[SIDE] speaker on " + (found < 0 ? "LEFT" : "RIGHT"));
                        }
                        return found;
                    }
                    t = t.parent;
                }
                if (!_loggedSideMiss)
                {
                    _loggedSideMiss = true;
                    TextFitPlugin.Logger.LogWarning("[SIDE] no LeftAvatar/RightAvatar found");
                }
            }
            catch (Exception e) { TextFitPlugin.Logger.LogWarning("[SIDE] error: " + e.Message); }
            return 0;
        }

        private int SearchAvatars(Transform node, int depth)
        {
            if (depth < 0 || node == null) return 0;
            for (int i = 0; i < node.childCount; i++)
            {
                var c = node.GetChild(i);
                if (c.gameObject.activeInHierarchy)
                {
                    if (c.name == "LeftAvatar") return -1;
                    if (c.name == "RightAvatar") return 1;
                }
                int sub = SearchAvatars(c, depth - 1);
                if (sub != 0) return sub;
            }
            return 0;
        }

        // Character-count wrap: cheap and predictable, matches the character-per-line rule
        // the user described. Preserves sentence boundaries where possible.
        private static string RewrapForWidth(string text, int maxChars)
        {
            string flat = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            while (flat.Contains("  ")) flat = flat.Replace("  ", " ");
            flat = flat.Trim();
            if (flat.Length <= maxChars) return flat;

            // Split into sentences, then greedily pack sentences per line.
            var sents = new System.Collections.Generic.List<string>();
            int start = 0;
            for (int i = 0; i < flat.Length; i++)
            {
                char c = flat[i];
                if (c == '.' || c == '!' || c == '?' || c == '…')
                {
                    int end = i + 1;
                    // include trailing punctuation like '...' or ')' or '"'
                    while (end < flat.Length && (flat[end] == '.' || flat[end] == '"' || flat[end] == '\''))
                        end++;
                    sents.Add(flat.Substring(start, end - start).Trim());
                    // skip separator whitespace
                    while (end < flat.Length && flat[end] == ' ') end++;
                    start = end;
                    i = end - 1;
                }
            }
            if (start < flat.Length) sents.Add(flat.Substring(start).Trim());

            var lines = new System.Collections.Generic.List<string>();
            string cur = "";
            foreach (var s in sents)
            {
                if (s.Length == 0) continue;
                if (cur.Length == 0)
                {
                    // Sentence longer than the line: word-wrap internally
                    if (s.Length <= maxChars) cur = s;
                    else
                    {
                        var parts = WordWrap(s, maxChars);
                        for (int j = 0; j < parts.Count - 1; j++) lines.Add(parts[j]);
                        cur = parts[parts.Count - 1];
                    }
                }
                else
                {
                    string trial = cur + " " + s;
                    if (trial.Length <= maxChars) cur = trial;
                    else
                    {
                        lines.Add(cur);
                        if (s.Length <= maxChars) cur = s;
                        else
                        {
                            var parts = WordWrap(s, maxChars);
                            for (int j = 0; j < parts.Count - 1; j++) lines.Add(parts[j]);
                            cur = parts[parts.Count - 1];
                        }
                    }
                }
            }
            if (cur.Length > 0) lines.Add(cur);
            return string.Join("\n", lines.ToArray());
        }

        private static System.Collections.Generic.List<string> WordWrap(string s, int maxChars)
        {
            var result = new System.Collections.Generic.List<string>();
            string cur = "";
            foreach (var word in s.Split(' '))
            {
                string trial = cur.Length == 0 ? word : cur + " " + word;
                if (trial.Length <= maxChars) cur = trial;
                else
                {
                    if (cur.Length > 0) result.Add(cur);
                    cur = word;
                }
            }
            if (cur.Length > 0) result.Add(cur);
            return result;
        }

        // Collapse the authored line breaks, then start a new line only after a sentence
        // terminator. Keeps long sentences on one (wrapping) line instead of chopping them
        // at whatever position the Chinese original happened to break.
        private static string BreakOnSentences(string s)
        {
            string flat = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            while (flat.Contains("  ")) flat = flat.Replace("  ", " ");
            flat = flat.Trim();

            var sb = new System.Text.StringBuilder(flat.Length + 8);
            for (int i = 0; i < flat.Length; i++)
            {
                char c = flat[i];
                sb.Append(c);
                if ((c == '.' || c == '!' || c == '?') && i + 1 < flat.Length)
                {
                    // don't break inside "Mr." style abbreviations or decimals
                    int j = i + 1;
                    while (j < flat.Length && flat[j] == '"') { sb.Append(flat[j]); j++; i++; }
                    if (j < flat.Length && flat[j] == ' ')
                    {
                        sb.Append('\n');
                        i = j;              // consume the space
                    }
                }
            }
            return sb.ToString();
        }

        // "Setting*" exempts Setting, Setting (1), Setting (2)... in one entry.
        private bool MatchesWildcard(string goName)
        {
            foreach (var e in _noShrink)
            {
                if (e.Length > 1 && e[e.Length - 1] == '*'
                    && goName.StartsWith(e.Substring(0, e.Length - 1)))
                    return true;
            }
            return false;
        }

        // "NameText:280" widens a component's rect so long text fits at full size
        // instead of being shrunk or clipped.
        private void ApplyWidening(Text t, string goName)
        {
            float want;
            if (!_widen.TryGetValue(goName, out want)) return;
            if (!_widened.Add(t.GetInstanceID())) return;
            try
            {
                var rt = t.rectTransform;
                float before = rt.rect.width;

                // Diagnostic: if the text is still cut after widening, the limit is not the
                // rect - it is being overlapped or clipped by something else.
                string parent = "?";
                try { parent = rt.parent == null ? "<root>" : rt.parent.name; } catch { }
                TextFitPlugin.Logger.LogInfo(
                    "[WIDEN] " + goName + " rectW=" + ((int)before) +
                    " prefW=" + ((int)t.preferredWidth) +
                    " anchorX=" + ((int)rt.anchoredPosition.x) +
                    " pivotX=" + rt.pivot.x + " align=" + t.alignment +
                    " parent=" + parent + " text='" + t.text + "'");

                if (before < want)
                {
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, want);

                    // Grow leftwards: the nameplate sits at the right of the screen against
                    // the portrait, so extra width added to the right just runs underneath it.
                    if (_widenLeft.Contains(goName))
                    {
                        float delta = want - before;
                        var p = rt.anchoredPosition;
                        p.x -= delta * (1f - rt.pivot.x);
                        rt.anchoredPosition = p;
                    }
                    TextFitPlugin.Logger.LogInfo(
                        "Widened " + goName + " " + ((int)before) + " -> " + ((int)want) + "px"
                        + (_widenLeft.Contains(goName) ? " (leftwards)" : ""));
                }
            }
            catch (Exception e) { TextFitPlugin.Logger.LogWarning("Widen failed: " + e.Message); }
        }

        // "NameText:300" or "NameText:300:left" (grow leftwards, keeping the right edge)
        private void ParseWiden()
        {
            foreach (var raw in TextFitPlugin.WidenObjects.Split(new[] { ',' },
                                    StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = raw.Trim().Split(':');
                if (parts.Length < 2) continue;
                float px;
                if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out px))
                    continue;
                var nm = parts[0].Trim();
                _widen[nm] = px;
                if (parts.Length > 2 && parts[2].Trim().ToLowerInvariant() == "left")
                    _widenLeft.Add(nm);
            }
        }

        private void ParseBodyObjects()
        {
            foreach (var raw in TextFitPlugin.BodyObjects.Split(new[] { ',' },
                                    StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = raw.Trim();
                int colon = entry.IndexOf(':');
                if (colon > 0)
                {
                    var nm = entry.Substring(0, colon).Trim();
                    float sc;
                    if (float.TryParse(entry.Substring(colon + 1).Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out sc))
                    {
                        _bodySet.Add(nm);
                        _bodyScale[nm] = sc;
                        continue;
                    }
                    entry = nm;
                }
                _bodySet.Add(entry);
            }
        }

        private void Update()
        {
            float dt;
            try { dt = Time.unscaledDeltaTime; }
            catch { return; }

            if (_failures > 5) return;

            // Sprites get their own fast pass: swapping a selected ring element must feel
            // immediate, while the text work (which walks every Text component) stays on
            // the slower interval so it doesn't cost frames.
            _spriteTimer += dt;
            if (_spriteTimer >= TextFitPlugin.SpriteRescan)
            {
                _spriteTimer = 0f;
                try { ScanSprites(); }
                catch (Exception e)
                {
                    _failures++;
                    TextFitPlugin.Logger.LogWarning("Sprite scan failed (" + _failures + "): " + e.Message);
                }
            }

            // Dialogue fast-path. Run EVERY frame (not throttled) so there is no visible
            // flicker between the game setting a small font and us restoring 38pt.
            // FindObjectsOfType<Text> on ~200 components per frame is cheap.
            try { EnforceDialogueSize(); }
            catch { }

            // Quest panel dynamic layout: the sidebar's TargetWrapper (grey objective
            // list) has a fixed anchored Y that assumes short CJK description text.
            // English descriptions wrap to more lines and overflow into it. Shift the
            // TargetWrapper down by however much the description overflows its rect.
            try { AdjustQuestPanelLayout(); }
            catch { }

            // Save-slot layout: hoist ElapsedTime up onto the same Y as Title so
            // "0h 19m" sits next to "Wuming Valley" instead of stacking above the Date.
            try { AdjustSaveSlotLayout(); }
            catch { }

            _timer += dt;
            if (_timer < TextFitPlugin.Rescan) return;
            _timer = 0f;

            try
            {
                Scan();
            }
            catch (Exception e)
            {
                _failures++;
                TextFitPlugin.Logger.LogWarning("Scan failed (" + _failures + "): " + e.Message);
            }
        }

        private int _dialogFastLogged = -1;
        private int _dialogFastTmpLogged = -1;
        private bool _tmpDumpDone;
        // Cache TargetWrapper's original anchoredPosition.y so we shift RELATIVE to
        // it and can restore to zero-shift when the description is short.
        private readonly System.Collections.Generic.Dictionary<int, float> _targetWrapperOrigY =
            new System.Collections.Generic.Dictionary<int, float>();
        private int _questLayoutLogged = -1;

        private void AdjustQuestPanelLayout()
        {
            // Find every ContentWrapper in the scene (usually just one active on the
            // mission board). For each, locate its sibling TargetWrapper and shift its
            // Y down by however much the description Content overflows its own rect.
            var texts = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Text>());
            if (texts == null) return;
            int shifted = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                Text descText;
                try { descText = texts[i].TryCast<Text>(); }
                catch { continue; }
                if (descText == null) continue;
                try
                {
                    var go = descText.gameObject;
                    // We want the description Text, whose parent is named ContentWrapper.
                    var parentTr = go.transform.parent;
                    if (parentTr == null || parentTr.name != "ContentWrapper") continue;

                    var wrapperRT = parentTr.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
                    if (wrapperRT == null) continue;
                    float wrapperH = wrapperRT.rect.height;
                    if (wrapperH <= 0f) continue;
                    float prefH = descText.preferredHeight;
                    if (prefH <= 0f) continue;

                    // Overflow: how many extra pixels the wrapped English needs beyond
                    // the authored (CJK-sized) container. Zero if it fits.
                    float overflow = prefH - wrapperH;
                    if (overflow < 0f) overflow = 0f;

                    // Locate TargetWrapper sibling under the same MissionBoard parent.
                    var grandparent = parentTr.parent;
                    if (grandparent == null) continue;
                    Transform targetWrapperTr = null;
                    for (int c = 0; c < grandparent.childCount; c++)
                    {
                        var sib = grandparent.GetChild(c);
                        if (sib != null && sib.name == "TargetWrapper") { targetWrapperTr = sib; break; }
                    }
                    if (targetWrapperTr == null) continue;

                    var twRT = targetWrapperTr.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
                    if (twRT == null) continue;

                    int twId = twRT.GetInstanceID();
                    float origY;
                    if (!_targetWrapperOrigY.TryGetValue(twId, out origY))
                    {
                        origY = twRT.anchoredPosition.y;
                        _targetWrapperOrigY[twId] = origY;
                    }
                    // Pivot-agnostic: shift downward = negative Y in Canvas UI space.
                    float targetY = origY - overflow;
                    var ap = twRT.anchoredPosition;
                    if (Mathf.Abs(ap.y - targetY) > 0.5f)
                    {
                        twRT.anchoredPosition = new Vector2(ap.x, targetY);
                        shifted++;
                    }

                    if (_questLayoutLogged != (int)overflow)
                    {
                        _questLayoutLogged = (int)overflow;
                        TextFitPlugin.Logger.LogInfo(
                            "[QUEST-LAYOUT] desc prefH=" + ((int)prefH) + " wrapperH=" + ((int)wrapperH) +
                            " overflow=" + ((int)overflow) + " -> TargetWrapper y=" + targetY);
                    }
                }
                catch { }
            }
        }

        // Align ElapsedTime's Y to Title's Y within each SaveItem, so "0h 19m"
        // sits on the same row as the map name instead of stacked over the Date.
        private void AdjustSaveSlotLayout()
        {
            var texts = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Text>());
            if (texts == null) return;
            for (int i = 0; i < texts.Length; i++)
            {
                Text et;
                try { et = texts[i].TryCast<Text>(); }
                catch { continue; }
                if (et == null) continue;
                try
                {
                    if (et.gameObject.name != "ElapsedTime") continue;
                    // Find sibling Title under the same SaveItem parent.
                    var parent = et.transform.parent;
                    if (parent == null) continue;
                    Transform titleTr = null;
                    for (int c = 0; c < parent.childCount; c++)
                    {
                        var sib = parent.GetChild(c);
                        if (sib != null && sib.name == "Title") { titleTr = sib; break; }
                    }
                    if (titleTr == null) continue;
                    var titleRT = titleTr.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
                    var etRT = et.transform.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
                    if (titleRT == null || etRT == null) continue;
                    float wantY = titleRT.anchoredPosition.y;
                    var ap = etRT.anchoredPosition;
                    if (Mathf.Abs(ap.y - wantY) > 0.5f)
                        etRT.anchoredPosition = new Vector2(ap.x, wantY);
                }
                catch { }
            }
        }

        private void DumpTree(Transform t, int depth, int maxDepth)
        {
            if (t == null || depth > maxDepth) return;
            string indent = new string(' ', depth * 2);
            var names = new System.Text.StringBuilder();
            try
            {
                // Non-generic form -- Il2CppInterop's generic GetComponents<T>() throws.
                var comps = t.gameObject.GetComponents(Il2CppType.Of<Component>());
                if (comps != null)
                {
                    for (int i = 0; i < comps.Length; i++)
                    {
                        if (comps[i] == null) continue;
                        if (names.Length > 0) names.Append(",");
                        try { names.Append(comps[i].GetIl2CppType().Name); }
                        catch { names.Append("?"); }
                    }
                }
            }
            catch (Exception e) { names.Append("<err:" + e.Message + ">"); }
            TextFitPlugin.Logger.LogInfo("[DIALOG-TREE] " + indent + t.name + " [" + names + "]");
            int cc = 0;
            try { cc = t.childCount; } catch { }
            for (int i = 0; i < cc; i++)
            {
                Transform c = null;
                try { c = t.GetChild(i); } catch { }
                if (c != null) DumpTree(c, depth + 1, maxDepth);
            }
        }

        private void EnforceDialogueSize()
        {
            // MUST use the non-generic Il2CppType.Of<T>() form -- the generic overload
            // hits Il2CppInterop's broken GenericMethod resolution and throws silently.
            var legacy = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<UnityEngine.UI.Text>());
            int hits = 0, total = legacy == null ? 0 : legacy.Length;
            for (int i = 0; i < total; i++)
            {
                Text t;
                try { t = legacy[i].TryCast<Text>(); }
                catch { continue; }
                if (t == null) continue;
                try
                {
                    // Match on either (a) the legacy TalkText by name, OR (b) any UI Text
                    // whose immediate parent name matches DialogueParentName ("Balabala").
                    // The Balabala/Text component is the ACTUAL visible dialogue; TalkText
                    // is a hidden older widget we've been fixing to no visible effect.
                    bool matchByName = t.gameObject.name == TextFitPlugin.SpeakerTextObject;
                    bool matchByParent = false;
                    try
                    {
                        var pt = t.transform.parent;
                        matchByParent = pt != null && pt.name == TextFitPlugin.DialogueParentName;
                    }
                    catch { }
                    if (!matchByName && !matchByParent) continue;

                    // Diagnostic snapshot BEFORE our set. Rate-limited to first change per
                    // (name, fontSize, localScale.x) tuple so the log doesn't flood.
                    float sx = 1f;
                    try { sx = t.transform.localScale.x; } catch { }
                    int fs = t.fontSize;
                    string diagKey = "DIAG-TALK:" + t.gameObject.name + "|" + fs + "|" + sx.ToString("F2");
                    if (_seen.Add(diagKey))
                        TextFitPlugin.Logger.LogInfo("[DIALOG-DIAG] '" + t.gameObject.name + "' pre-set fontSize=" + fs + " scale.x=" + sx.ToString("F3") + " text-len=" + (t.text == null ? 0 : t.text.Length));

                    if (t.resizeTextForBestFit) t.resizeTextForBestFit = false;
                    if (t.fontSize != TextFitPlugin.DialogueFontSize)
                        t.fontSize = TextFitPlugin.DialogueFontSize;
                    // Force scale to 1 in case the game shrinks via RectTransform.localScale.
                    if (sx != 1f)
                    {
                        try { t.transform.localScale = Vector3.one; } catch { }
                    }
                    hits++;

                    // Walk up to find Font_Control on an ancestor and disable it. That component
                    // is on Button_UI (dialogue-box root) and is the most likely thing resetting
                    // fontSize per new line based on some content-length heuristic.
                    try
                    {
                        Transform anc = t.transform.parent;
                        for (int u = 0; u < 6 && anc != null; u++)
                        {
                            var comps = anc.gameObject.GetComponents(Il2CppType.Of<Component>());
                            if (comps != null)
                            {
                                for (int c = 0; c < comps.Length; c++)
                                {
                                    if (comps[c] == null) continue;
                                    string tn = "?";
                                    try { tn = comps[c].GetIl2CppType().Name; } catch { }
                                    if (tn == "Font_Control")
                                    {
                                        var beh = comps[c].TryCast<Behaviour>();
                                        if (beh != null && beh.enabled)
                                        {
                                            beh.enabled = false;
                                            if (_seen.Add("DISABLED-FONTCTL:" + anc.name))
                                                TextFitPlugin.Logger.LogInfo("[DIALOG-DIAG] disabled Font_Control on '" + anc.name + "'");
                                        }
                                    }
                                }
                            }
                            anc = anc.parent;
                        }
                    }
                    catch { }
                }
                catch { }
            }
            if (hits != _dialogFastLogged)
            {
                _dialogFastLogged = hits;
                TextFitPlugin.Logger.LogInfo(
                    "[DIALOG-FAST] found " + hits + " of " + total + " Text components named '" +
                    TextFitPlugin.SpeakerTextObject + "'");
            }

            // The visible dialogue in this game is TextMeshProUGUI, not legacy Text --
            // the only "TalkText" legacy Text is a hidden Button label. Enumerate the
            // TMP UGUI components too and enforce our size on any named TalkText.
            // Also disable auto-sizing (TMP's equivalent of resizeTextForBestFit) so
            // Unity's own resizer can't shrink our value away.
            var tmps = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<TMPro.TextMeshProUGUI>());
            int tmpHits = 0, tmpTotal = tmps == null ? 0 : tmps.Length;
            for (int i = 0; i < tmpTotal; i++)
            {
                TMPro.TextMeshProUGUI t;
                try { t = tmps[i].TryCast<TMPro.TextMeshProUGUI>(); }
                catch { continue; }
                if (t == null) continue;
                try
                {
                    if (t.gameObject.name != TextFitPlugin.SpeakerTextObject) continue;
                    if (t.enableAutoSizing) t.enableAutoSizing = false;
                    if (t.fontSize != TextFitPlugin.DialogueFontSize)
                        t.fontSize = TextFitPlugin.DialogueFontSize;
                    tmpHits++;
                }
                catch { }
            }
            if (tmpHits != _dialogFastTmpLogged)
            {
                _dialogFastTmpLogged = tmpHits;
                TextFitPlugin.Logger.LogInfo(
                    "[DIALOG-FAST-TMP] found " + tmpHits + " of " + tmpTotal + " TMP UGUI components named '" +
                    TextFitPlugin.SpeakerTextObject + "'");
            }

            // One-time diagnostic dump: if we couldn't find TalkText in either legacy
            // Text or TMP but the scene has TMP components, dump the first ~40 of them
            // so we can identify what the dialogue body is actually called.
            if (!_tmpDumpDone && tmpTotal > 0)
            {
                _tmpDumpDone = true;
                int dumpMax = Math.Min(40, tmpTotal);
                for (int i = 0; i < dumpMax; i++)
                {
                    try
                    {
                        var t = tmps[i].TryCast<TMPro.TextMeshProUGUI>();
                        if (t == null) continue;
                        string nm = t.gameObject.name;
                        string p = "?";
                        try { if (t.transform.parent != null) p = t.transform.parent.name; } catch { }
                        string tx = t.text == null ? "" : (t.text.Length > 40 ? t.text.Substring(0, 40) : t.text);
                        tx = tx.Replace("\n", " ").Replace("\r", " ");
                        TextFitPlugin.Logger.LogInfo("[TMP-DUMP] name='" + nm + "' parent='" + p + "' fontSize=" + t.fontSize + " autoSize=" + t.enableAutoSizing + " text='" + tx + "'");
                    }
                    catch { }
                }
            }
        }

        private Font _osFont;
        private bool _osFontTried;

        // sprite name -> replacement loaded from BepInEx\plugins\TextFit_Sprites\<name>.png
        private readonly System.Collections.Generic.Dictionary<string, Sprite> _spriteCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();
        private System.Collections.Generic.HashSet<string> _spriteFiles;

        private Sprite GetReplacement(string spriteName)
        {
            if (_spriteFiles == null)
            {
                _spriteFiles = new System.Collections.Generic.HashSet<string>();
                try
                {
                    if (System.IO.Directory.Exists(TextFitPlugin.SpriteDir))
                    {
                        foreach (var f in System.IO.Directory.GetFiles(TextFitPlugin.SpriteDir, "*.png"))
                            _spriteFiles.Add(System.IO.Path.GetFileNameWithoutExtension(f));
                        TextFitPlugin.Logger.LogInfo("Sprite replacements available: " + _spriteFiles.Count);
                    }
                }
                catch (Exception e) { TextFitPlugin.Logger.LogWarning("Sprite dir scan failed: " + e.Message); }
            }

            Sprite cached;
            if (_spriteCache.TryGetValue(spriteName, out cached)) return cached;

            string file = spriteName;
            if (!_spriteFiles.Contains(file))
            {
                // Selected/highlight states are separate sprites named like the base one
                // (e.g. UI_Wune_P1 -> UI_Wune_P1_On). Fall back to the longest base name that
                // prefixes this sprite, so variants pick up the English art automatically.
                string best = null;
                foreach (var f in _spriteFiles)
                    if (spriteName.StartsWith(f) && (best == null || f.Length > best.Length))
                        best = f;
                if (best == null)
                {
                    _spriteCache[spriteName] = null;
                    return null;
                }
                file = best;
                TextFitPlugin.Logger.LogInfo(
                    "[VARIANT] '" + spriteName + "' matched base '" + best + "'");
            }

            Sprite baseCached;
            if (!file.Equals(spriteName) && _spriteCache.TryGetValue(file, out baseCached))
            {
                _spriteCache[spriteName] = baseCached;
                return baseCached;
            }
            spriteName = file;

            try
            {
                byte[] png = System.IO.File.ReadAllBytes(
                    System.IO.Path.Combine(TextFitPlugin.SpriteDir, spriteName + ".png"));
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, png))
                {
                    TextFitPlugin.Logger.LogWarning("LoadImage failed for " + spriteName);
                    _spriteCache[spriteName] = null;
                    return null;
                }
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply();
                var sp = Sprite.Create(tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sp.hideFlags = HideFlags.HideAndDontSave;
                tex.hideFlags = HideFlags.HideAndDontSave;
                _spriteCache[spriteName] = sp;
                TextFitPlugin.Logger.LogInfo("Loaded replacement sprite " + spriteName +
                                             " (" + tex.width + "x" + tex.height + ")");
                return sp;
            }
            catch (Exception e)
            {
                TextFitPlugin.Logger.LogWarning("Sprite load failed for " + spriteName + ": " + e.Message);
                _spriteCache[spriteName] = null;
                return null;
            }
        }

        private Font GetOSFont()
        {
            if (_osFontTried) return _osFont;
            _osFontTried = true;
            if (TextFitPlugin.OSFontName.Length == 0) return null;
            try
            {
                // Bind by reflection: a direct call fails to JIT because the sibling
                // CreateDynamicFontFromOSFont(Il2CppStringArray,int) overload carries an invalid
                // generic signature (Il2CppReferenceArray<string> breaks its own constraint).
                var mi = typeof(Font).GetMethod("CreateDynamicFontFromOSFont",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(int) }, null);
                if (mi == null)
                {
                    TextFitPlugin.Logger.LogWarning("CreateDynamicFontFromOSFont(string,int) not found.");
                    return null;
                }
                _osFont = mi.Invoke(null, new object[] { TextFitPlugin.OSFontName, 32 }) as Font;
                if (_osFont != null)
                    TextFitPlugin.Logger.LogInfo("OS font family loaded: '" + _osFont.name + "'");
                else
                    TextFitPlugin.Logger.LogWarning("OS font '" + TextFitPlugin.OSFontName + "' not found; falling back to embedded asset.");
            }
            catch (Exception e)
            {
                TextFitPlugin.Logger.LogWarning("OS font load failed: " + e.Message);
                _osFont = null;
            }
            return _osFont;
        }

        private void Scan()
        {
            if (!_parsedBodyObjects)
            {
                _parsedBodyObjects = true;
                ParseBodyObjects();
                ParseWiden();
            }

            // Non-generic lookup: avoids il2cpp generic-method resolution entirely.
            // NOTE: must NOT use Resources.FindObjectsOfTypeAll here - it walks every loaded
            // object including assets (this game ships a 3 GB resources.assets) and starves
            // the main thread during loading. FindObjectsOfType only walks active scene objects.
            var all = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<TMP_Text>());
            if (all == null) return;
            if (!_loggedFirstScan)
            {
                _loggedFirstScan = true;
                TextFitPlugin.Logger.LogInfo("First scan ran; " + all.Length + " text components visible.");
            }

            int changed = 0;
            for (int i = 0; i < all.Length; i++)
            {
                TMP_Text t;
                try { t = all[i].TryCast<TMP_Text>(); }
                catch { continue; }
                if (t == null) continue;

                bool touched = false;
                try
                {
                    string goName = t.gameObject.name;
                    if (_seen.Add("TMP:" + goName))
                    {
                        float w0 = 0f;
                        try { w0 = t.rectTransform.rect.width; } catch { }
                        string fa = "?";
                        try { if (t.font != null) fa = t.font.name; } catch { }
                        TextFitPlugin.Logger.LogInfo(
                            "[TMP] obj=" + goName + " font=" + fa + " rectW=" + ((int)w0) +
                            " len=" + (t.text == null ? 0 : t.text.Length));
                    }

                    if (TextFitPlugin.DoRichText && !t.richText)
                    {
                        t.richText = true;
                        touched = true;
                    }

                    if (TextFitPlugin.DoAutoSize && !t.enableAutoSizing)
                    {
                        float baseSize = t.fontSize;
                        if (baseSize > 1f)
                        {
                            t.fontSizeMax = baseSize;
                            t.fontSizeMin = Mathf.Max(6f, baseSize * TextFitPlugin.MinRatio);
                            t.enableAutoSizing = true;
                            touched = true;
                        }
                    }

                    // A short label in a narrow box (e.g. the golden skill names in Arts) should
                    // shrink to one line rather than stack into a column. With word wrapping off,
                    // auto-sizing has to solve for width instead of adding lines.
                    int tlen = t.text == null ? 0 : t.text.Length;
                    float rw2 = 0f;
                    try { rw2 = t.rectTransform.rect.width; } catch { }
                    if (TextFitPlugin.NoWrapShortLabels && tlen > 0 && tlen <= 40
                        && rw2 > 0f && rw2 < 260f && t.enableWordWrapping)
                    {
                        t.enableWordWrapping = false;
                        t.fontSizeMin = Mathf.Max(6f, t.fontSizeMax * 0.45f);
                        touched = true;
                        TextFitPlugin.Logger.LogInfo("No-wrap + shrink applied to TMP '" + goName + "'");
                    }
                }
                catch { continue; }

                if (touched) changed++;
            }

            // Image components are handled by ScanSprites() on its own faster timer.

            // Legacy UnityEngine.UI.Text - this is what the stat/menu labels actually use.
            ScanText();
        }

        // Sprite swapping only - cheap enough to run several times a second so a
        // selected ring element changes without a visible delay.
        private void ScanSprites()
        {
            {
                var imgs = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Image>());
                if (imgs != null)
                {
                    for (int i = 0; i < imgs.Length; i++)
                    {
                        Image im;
                        try { im = imgs[i].TryCast<Image>(); } catch { continue; }
                        if (im == null) continue;
                        try
                        {
                            string sn = im.sprite == null ? "<none>" : im.sprite.name;
                            if (sn == "<none>") continue;

                            // Swap brush-art labels for English versions loaded from disk.
                            // overrideSprite wins over sprite when set, so it must be handled too
                            // or the swap is invisible (this is why the right-hand column and the
                            // gold selected state stayed Chinese).
                            string osn = null;
                            try { osn = im.overrideSprite == null ? null : im.overrideSprite.name; } catch { }

                            Sprite replacement = GetReplacement(sn);
                            if (replacement != null && im.sprite != replacement)
                            {
                                im.sprite = replacement;
                                if (_seen.Add("SWAP:" + sn))
                                    TextFitPlugin.Logger.LogInfo("Sprite replaced: " + sn);
                            }
                            if (osn != null)
                            {
                                Sprite or = GetReplacement(osn);
                                if (or != null && im.overrideSprite != or)
                                {
                                    im.overrideSprite = or;
                                    if (_seen.Add("OSWAP:" + osn))
                                        TextFitPlugin.Logger.LogInfo("overrideSprite replaced: " + osn);
                                }
                                else if (or == null && _seen.Add("OIMG:" + im.gameObject.name + "|" + osn))
                                {
                                    TextFitPlugin.Logger.LogInfo(
                                        "[IMG-OVERRIDE] obj=" + im.gameObject.name + " sprite=" + osn);
                                }
                            }
                            if (replacement != null) continue;

                            // ONE-TIME diagnostic: when we see the dialogue's "next" arrow, dump
                            // the whole dialogue box subtree so we can see what renders the body
                            // text. Neither legacy Text nor TextMeshProUGUI scans find it, so the
                            // component type must be something else -- this will show what.
                            if (sn == "UI_TalkNextStep" && _seen.Add("DIALOG-TREE2"))
                            {
                                try
                                {
                                    Transform root = im.transform;
                                    for (int u = 0; u < 4 && root.parent != null; u++) root = root.parent;
                                    TextFitPlugin.Logger.LogInfo("[DIALOG-TREE] rooted at '" + root.name + "'");
                                    DumpTree(root, 0, 6);
                                }
                                catch (Exception e)
                                { TextFitPlugin.Logger.LogWarning("DumpTree failed: " + e.Message); }
                            }

                            string key = im.gameObject.name + "|" + sn;
                            if (!_seen.Add("IMG:" + key)) continue;
                            float w = 0f, h = 0f;
                            try { w = im.rectTransform.rect.width; h = im.rectTransform.rect.height; } catch { }
                            if (TextFitPlugin.LogSprites)
                                TextFitPlugin.Logger.LogInfo(
                                    "[IMG] obj=" + im.gameObject.name + " sprite=" + sn +
                                    " size=" + ((int)w) + "x" + ((int)h));
                        }
                        catch { continue; }
                    }
                }
            }
        }

        private void ScanText()
        {
            int changed = 0;

            // Legacy UnityEngine.UI.Text - this is what the stat/menu labels actually use.
            // TMP's enableAutoSizing does not exist here; the equivalent is resizeTextForBestFit.
            var legacy = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Text>());
            if (legacy != null)
            {
                for (int i = 0; i < legacy.Length; i++)
                {
                    Text t;
                    try { t = legacy[i].TryCast<Text>(); }
                    catch { continue; }
                    if (t == null) continue;

                    bool touched = false;
                    try
                    {
                        // Diagnostic: record what each label actually is, once per object name.
                        string goName = t.gameObject.name;
                        // key on width as well: several distinct components share a name
                        if (_seen.Add(goName + "#" + ((int)(t.rectTransform.rect.width))))
                        {
                            string fontName = "?";
                            try { if (t.font != null) fontName = t.font.name; } catch { }
                            float w = 0f;
                            try { w = t.rectTransform.rect.width; } catch { }
                            string p1 = "?", p2 = "?";
                            try
                            {
                                var parent = t.rectTransform.parent;
                                p1 = parent == null ? "<root>" : parent.name;
                                var gp = parent == null ? null : parent.parent;
                                p2 = gp == null ? "<root>" : gp.name;
                            }
                            catch { }
                            string preview = t.text == null ? "" :
                                (t.text.Length > 26 ? t.text.Substring(0, 26) : t.text)
                                    .Replace("\r", " ").Replace("\n", " ");
                            TextFitPlugin.Logger.LogInfo(
                                "[UIText] obj=" + goName + " font=" + fontName +
                                " rectW=" + ((int)w) + " overflow=" + t.horizontalOverflow +
                                " len=" + (t.text == null ? 0 : t.text.Length) +
                                " parent=" + p1 + "/" + p2 + " text='" + preview + "'");
                        }

                        // Remember every font we meet, so we can reassign by name later.
                        try
                        {
                            if (t.font != null && !_fonts.ContainsKey(t.font.name))
                                _fonts[t.font.name] = t.font;
                        }
                        catch { }

                        // Body text (dialogue, descriptions) -> body font; headings keep theirs.
                        bool isBody = _bodySet.Contains(goName);
                        if (isBody)
                        {
                            // Prefer a Windows-installed FAMILY: with a real Bold 700 face present,
                            // Unity resolves <b> to true bold instead of smearing the regular weight
                            // (faux bold keeps the original advance widths, which looks cramped).
                            Font wanted = GetOSFont();
                            string wantedName = TextFitPlugin.OSFontName;
                            if (wanted == null && TextFitPlugin.BodyFontName.Length > 0)
                            {
                                _fonts.TryGetValue(TextFitPlugin.BodyFontName, out wanted);
                                wantedName = TextFitPlugin.BodyFontName;
                            }

                            if (wanted != null && t.font != null && t.font.name != wanted.name)
                            {
                                t.font = wanted;
                                touched = true;
                                TextFitPlugin.Logger.LogInfo("Body font '" + wanted.name + "' applied to " + goName);
                            }

                            // Aleo has a much larger x-height than the CJK font it replaces, so at
                            // the same point size it renders visibly bigger and overruns the panel.
                            // Single deterministic sizing path for body text.
                            // _origSize always holds the TRUE original (captured once, before any
                            // change); the working size is derived from it every time. Deriving from
                            // the current size instead is what let the earlier version undo its own
                            // BodyFontScale and spring the descriptions back to full size.
                            int bid = t.GetInstanceID();
                            int trueOrig;
                            if (!_origSize.TryGetValue(bid, out trueOrig))
                            {
                                trueOrig = t.fontSize;
                                _origSize[bid] = trueOrig;
                            }
                            // Dialogue is authoritatively sized from config. Reading fontSize live
                            // is unreliable: the game bumps it up on some emphasis lines and shrinks
                            // it on others, and instance recreation after a scene change resamples
                            // whatever value the game happens to have set at that moment. Either way
                            // we end up drifting - too small OR too big - for a few lines until the
                            // state naturally resets. Fixed value = no drift.
                            bool isDialogue = goName == TextFitPlugin.SpeakerTextObject;

                            if (trueOrig > 1)
                            {
                                // A per-object scale may be given as "Content:0.7" in the config;
                                // the panel's Text rect is often taller than the painted scroll, so
                                // height-fitting alone cannot tell that the text overflows visually.
                                float objScale;
                                if (!_bodyScale.TryGetValue(goName, out objScale))
                                    objScale = TextFitPlugin.BodyFontScale;
                                // Sub-item override: quest-objective list ("Content" under
                                // parent="Objective") is rendered by the SAME GameObject name as
                                // the quest description, so name alone cannot separate them --
                                // look at the parent to shrink only the sub-item version.
                                bool isSubItem = false;
                                if (_subItemParents.Count > 0)
                                {
                                    try
                                    {
                                        var parentTr = t.transform.parent;
                                        if (parentTr != null && _subItemParents.Contains(parentTr.name))
                                        {
                                            objScale = TextFitPlugin.SubItemBodyScale;
                                            isSubItem = true;
                                        }
                                    }
                                    catch { }
                                }

                                int baseSize = isDialogue
                                    ? TextFitPlugin.DialogueFontSize
                                    : Mathf.Max(6, (int)Mathf.Round(trueOrig * objScale));

                                // Dialogue + sub-item: disable Unity's built-in best-fit and enforce
                                // our size every scan. Best-fit is what makes non-dialogue panels
                                // (quest desc, location blurb) squeeze translated text into fixed
                                // containers, so we leave it on for THOSE. But a sub-item that keeps
                                // best-fit renders as large as the container allows -- ignoring our
                                // baseSize -- which is why SubItemBodyScale had no visible effect.
                                string lastBody;
                                bool bodyTextChanged = !_lastText.TryGetValue(bid, out lastBody) || lastBody != t.text;

                                bool enforceExact = isDialogue || isSubItem;
                                if (enforceExact)
                                {
                                    if (t.resizeTextForBestFit)
                                    {
                                        t.resizeTextForBestFit = false;
                                        touched = true;
                                    }
                                    if (t.fontSize != baseSize)
                                    {
                                        t.fontSize = baseSize;
                                        touched = true;
                                    }
                                }

                                if (bodyTextChanged)
                                {
                                    _lastText[bid] = t.text;

                                    if (!enforceExact && t.fontSize != baseSize)
                                    {
                                        t.fontSize = baseSize;
                                        touched = true;
                                    }

                                    if (_seen.Add("BODY:" + goName))
                                    {
                                        float dh = 0f, dp = 0f;
                                        try { dh = t.rectTransform.rect.height; dp = t.preferredHeight; } catch { }
                                        TextFitPlugin.Logger.LogInfo(
                                            "[BODY] obj=" + goName + " orig=" + trueOrig +
                                            " scale=" + objScale + " base=" + baseSize +
                                            " rectH=" + ((int)dh) + " prefH=" + ((int)dp) +
                                            " len=" + (t.text == null ? 0 : t.text.Length));
                                    }

                                    // English runs far longer than the Chinese these panels were
                                    // drawn for, so shrink further if it still overflows vertically.
                                    // NEVER on dialogue: TalkText has SideAwareWrap for sizing;
                                    // if the game grows its rect for a wrapped line, height-fit
                                    // would shrink the font and it would stay small next line.
                                    if (TextFitPlugin.FitBodyHeight && !isDialogue)
                                    {
                                        float rh = 0f;
                                        try { rh = t.rectTransform.rect.height; } catch { }
                                        // Only height-fit real containers. A dialogue rect at ~30px
                                        // is a single scrolling line, not a fixed panel; treating it
                                        // as one made TalkText shrink to ~27pt whenever the line ran
                                        // more than one row - the "randomly small dialogue" bug.
                                        if (rh >= 60f)
                                        {
                                            float ph = t.preferredHeight;
                                            if (ph > rh)
                                            {
                                                float sc = rh / ph;
                                                if (sc < TextFitPlugin.MaxShrink) sc = TextFitPlugin.MaxShrink;
                                                int ns2 = Mathf.Max(TextFitPlugin.MinLabelSize,
                                                                    (int)(baseSize * sc));
                                                if (ns2 < baseSize)
                                                {
                                                    t.fontSize = ns2;
                                                    touched = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (TextFitPlugin.TitleFontName.Length > 0 && _titleSet.Contains(goName))
                        {
                            // Headings/names use the display face (speaker nameplate, titles).
                            // No sizing here - these keep their authored size.
                            Font tf;
                            if (_fonts.TryGetValue(TextFitPlugin.TitleFontName, out tf)
                                && tf != null && t.font != null
                                && t.font.name != TextFitPlugin.TitleFontName)
                            {
                                t.font = tf;
                                touched = true;
                                TextFitPlugin.Logger.LogInfo("Title font '" + tf.name + "' applied to " + goName);
                            }
                        }

                        if (TextFitPlugin.DoRichText && !t.supportRichText)
                        {
                            t.supportRichText = true;
                            touched = true;
                        }

                        // The translation is hard-wrapped for the dialogue box (which overflows a
                        // narrow rect and cannot wrap itself). A panel that DOES wrap re-wraps those
                        // baked breaks into ragged one-word lines, so hand it flowing text instead.
                        if (isBody && t.horizontalOverflow == HorizontalWrapMode.Wrap)
                        {
                            string s = t.text;
                            if (s != null && s.IndexOf('\n') >= 0)
                            {
                                t.text = s.Replace("\r\n", " ").Replace("\n", " ").Replace("  ", " ");
                                touched = true;
                            }
                        }

                        // Dialogue: rewrap based on speaker side.
                        // Baked in the translation is a wrap for the WORST case (right-side
                        // speaker whose portrait crowds the text). Left-side speakers have the
                        // whole width free, so we can un-bake and pack longer lines.
                        if (TextFitPlugin.SideAwareWrap && goName == TextFitPlugin.SpeakerTextObject)
                        {
                            string s = t.text;
                            if (!string.IsNullOrEmpty(s))
                            {
                                // If the semantic text (ignoring whitespace) changed, this is a
                                // new dialogue line - reset detection so we log the new side.
                                string norm = s.Replace("\r", "").Replace("\n", " ").Trim();
                                bool textChanged = norm != _lastTalkText;
                                if (textChanged)
                                {
                                    _lastTalkText = norm;
                                    _loggedSide = false;
                                    _loggedSideMiss = false;
                                    _lastTalkSide = DetectSpeakerSide(t);
                                }

                                int width = _lastTalkSide < 0 ? TextFitPlugin.LeftSpeakerLineWidth
                                          : _lastTalkSide > 0 ? TextFitPlugin.RightSpeakerLineWidth
                                          : 0;
                                if (width > 0)
                                {
                                    string reflowed = RewrapForWidth(s, width);
                                    if (reflowed != s)
                                    {
                                        t.text = reflowed;
                                        touched = true;
                                        if (textChanged)
                                            TextFitPlugin.Logger.LogInfo(
                                                "[REWRAP] side=" + _lastTalkSide + " width=" + width +
                                                " lines=" + (reflowed.Split('\n').Length));
                                    }
                                }
                            }
                        }

                        // Location blurbs keep the Chinese line breaks, which land mid-clause once
                        // translated. Re-break so a new line only starts after a sentence ends.
                        if (_sentenceBreak.Contains(goName))
                        {
                            string s = t.text;
                            if (!string.IsNullOrEmpty(s))
                            {
                                string reflowed = BreakOnSentences(s);
                                if (reflowed != s)
                                {
                                    t.text = reflowed;
                                    touched = true;
                                }
                            }
                        }

                        // Do NOT force Wrap: the dialogue box uses a deliberately narrow rect
                        // and overflows across a wide painted banner. Forcing wrap there turns
                        // dialogue into a one-word-per-line column.
                        // Best-fit only where it makes sense: short labels in small boxes.
                        int len = t.text == null ? 0 : t.text.Length;
                        float rw = 0f;
                        try { rw = t.rectTransform.rect.width; } catch { }
                        bool longTextInNarrowRect = (len > 30 && rw > 0f && rw < 260f);

                        ApplyWidening(t, goName);

                        // Fit labels to their own box.
                        //
                        // resizeTextForBestFit is not enough: it only has to fit the RECT, so a box
                        // two lines tall happily wraps "Wufang Flying / Sword" and calls it done.
                        // Measuring preferredWidth and scaling the point size keeps a label on ONE
                        // line, which is what these narrow boxes are drawn for. It also rescues
                        // labels that were being clipped (e.g. "Equipmen" in the Backpack tabs).
                        // Only short LABELS qualify. Long prose in a narrow rect would need a tiny
                        // scale factor and collapse to the floor - that is what made the weapon and
                        // skill descriptions microscopic. Those are left to wrap/overflow instead.
                        if (TextFitPlugin.DoAutoSize && !isBody && !longTextInNarrowRect
                            && !t.resizeTextForBestFit && rw > 8f
                            && len > 0 && len <= TextFitPlugin.MaxLabelChars)
                        {
                            int id = t.GetInstanceID();
                            string lastText;
                            bool textChanged = !_lastText.TryGetValue(id, out lastText) || lastText != t.text;
                            if (textChanged)
                            {
                                _lastText[id] = t.text;

                                int orig;
                                if (!_origSize.TryGetValue(id, out orig))
                                {
                                    orig = t.fontSize;
                                    _origSize[id] = orig;
                                }
                                // Exemptions may be width-qualified ("Name@170"), because several
                                // distinct components share a name - both the scroll title and the
                                // golden list entry are called "Name" but want opposite treatment.
                                bool exempt = _noShrink.Contains(goName)
                                              || _noShrink.Contains(goName + "@" + ((int)rw))
                                              || MatchesWildcard(goName);
                                if (orig > 1 && !exempt)
                                {
                                    if (t.fontSize != orig) t.fontSize = orig;   // measure at full size
                                    float pw = t.preferredWidth;
                                    if (pw > rw)
                                    {
                                        float scale = rw / pw;
                                        // Refuse to shrink past the readable limit; a slightly
                                        // clipped label beats an illegible one.
                                        if (scale < TextFitPlugin.MaxShrink)
                                            scale = TextFitPlugin.MaxShrink;
                                        int ns = Mathf.Max(TextFitPlugin.MinLabelSize,
                                                           (int)(orig * scale));
                                        if (ns < orig)
                                        {
                                            t.fontSize = ns;
                                            touched = true;
                                            // key on width too: several distinct components share
                                            // a name (both "Name" objects show the skill title)
                                            if (_seen.Add("SHRINK:" + goName + "|" + ((int)rw)))
                                                TextFitPlugin.Logger.LogInfo(
                                                    "[SHRINK] obj=" + goName + " " + orig + " -> " + ns +
                                                    "pt  rectW=" + ((int)rw) + "  text='" +
                                                    (t.text.Length > 28 ? t.text.Substring(0, 28) : t.text) + "'");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { continue; }

                    if (touched) changed++;
                }
            }

            if (changed > 0)
            {
                _total += changed;
                TextFitPlugin.Logger.LogInfo("Fitted " + changed + " text components (total " + _total + ").");
            }
        }
    }
}
