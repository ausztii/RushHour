#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The RushHour interface look, shared by the start screen and the playable HUD so the two can
/// never drift apart.
///
/// The reference layouts it follows are built from five things, and nothing else: a warm
/// near-black ink field, bone-cream type, a torn cream brush stroke marking the one selected
/// item, hairline rules separating rows, and boxed controls with amber and blood accents.
/// This class composes those pieces; the builders decide where they go. Nothing here runs at
/// play time - the HUD reads the same palette through the colours the builder writes into
/// HudController.
/// </summary>
public static class RushHourUiTheme
{
    // --------------------------------------------------------------------- ink
    /// <summary>The deep warm black every panel sits on.</summary>
    public static readonly Color Ink = new Color(0.039f, 0.027f, 0.024f, 1f);

    /// <summary>Ink at the weight used for panel fills, so the road still reads behind them.</summary>
    public static readonly Color Panel = new Color(0.075f, 0.051f, 0.043f, 0.88f);

    /// <summary>Ink at the weight used for the inside of framed controls and the result card.</summary>
    public static readonly Color PanelDeep = new Color(0.027f, 0.018f, 0.016f, 0.95f);

    /// <summary>Near-black for meter tracks, so a filling bar reads as a light source.</summary>
    public static readonly Color Track = new Color(0.020f, 0.014f, 0.012f, 0.95f);

    // ------------------------------------------------------------------- paper
    /// <summary>The cream of the selection stroke - the brightest thing on screen.</summary>
    public static readonly Color Bone = new Color(0.949f, 0.914f, 0.827f, 1f);

    /// <summary>Primary type colour.</summary>
    public static readonly Color Cream = new Color(0.918f, 0.878f, 0.784f, 1f);

    /// <summary>Secondary type: captions, bodies, units.</summary>
    public static readonly Color Muted = new Color(0.565f, 0.510f, 0.451f, 1f);

    /// <summary>Tertiary type: chrome, build tags, the bottom bar's right-hand note.</summary>
    public static readonly Color Faint = new Color(0.376f, 0.337f, 0.298f, 1f);

    // ------------------------------------------------------------------ accent
    /// <summary>The wordmark and label accent the game already used for its speed lines.</summary>
    public static readonly Color Amber = new Color(1f, 0.722f, 0.341f, 1f);

    /// <summary>Amber driven down into the ink, for charge mid-range and pressed states.</summary>
    public static readonly Color AmberDeep = new Color(0.647f, 0.396f, 0.133f, 1f);

    /// <summary>Failure red. Used for a lost shipment and nothing else.</summary>
    public static readonly Color Blood = new Color(0.784f, 0.204f, 0.129f, 1f);

    /// <summary>Blood sunk into the ink, for the vignette behind a failed run.</summary>
    public static readonly Color BloodDeep = new Color(0.294f, 0.063f, 0.043f, 1f);

    /// <summary>Hairline rule that separates rows and caps panels.</summary>
    public static readonly Color RuleInk = new Color(0.949f, 0.914f, 0.827f, 0.22f);

    /// <summary>Hairline at the weight used inside dense readouts.</summary>
    public static readonly Color RuleFaintInk = new Color(0.949f, 0.914f, 0.827f, 0.11f);

    /// <summary>Same colour at a different weight.</summary>
    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    /// <summary>
    /// Legacy Text has no letter-spacing, so the short labels are pre-spaced the way the
    /// reference layouts set them: one space between letters, three between words.
    /// </summary>
    public static string Spread(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        StringBuilder sb = new StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ' ') { sb.Append("   "); continue; }

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ pieces

    /// <summary>A plain filled rectangle. The atom every panel, rule and track is made of.</summary>
    public static GameObject Box(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        bool raycastTarget = false)
    {
        return RushHourSceneBuilder.CreateUIImage(parent, name, color, anchorMin, anchorMax, pivot,
            anchoredPos, sizeDelta, raycastTarget);
    }

    /// <summary>
    /// A block of type. Every label on screen goes through here so the drop shadow that keeps
    /// cream legible over sunlit road is applied in exactly one place.
    /// </summary>
    public static Text Type(Transform parent, string name, string content, Font font, int fontSize,
        TextAnchor anchor, Color color, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        bool shadow = true)
    {
        Text text = RushHourSceneBuilder.CreateUIText(parent, name, content, font, fontSize, anchor,
            color, style, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);

        if (shadow)
        {
            Shadow drop = text.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.62f);
            drop.effectDistance = new Vector2(0f, -3f);
        }

        return text;
    }

    /// <summary>A 2px bone hairline. The rule between rows in the reference layouts.</summary>
    public static GameObject Hairline(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        Color? color = null)
    {
        return Box(parent, name, color ?? RuleInk, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
    }

    /// <summary>
    /// The torn cream brush stroke behind the selected item. Composed from overlapping quads with
    /// jittered heights, widths and centres so it reads as a dry brush rather than a rectangle.
    /// Seeded, so a rebuild is byte-identical.
    /// </summary>
    public static GameObject Stroke(Transform parent, string name, Vector2 size, int seed,
        float alpha = 0.96f)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Random.State previous = Random.state;
        Random.InitState(seed);

        const int bands = 10;
        for (int i = 0; i < bands; i++)
        {
            // The last bands are shorter and fainter, which is what breaks the right-hand edge up
            // into the ragged tail the stroke is recognised by.
            float t = i / (float)(bands - 1);
            float width = size.x * Random.Range(0.80f, 1.04f);
            float height = size.y * Random.Range(0.70f, 1f) * Mathf.Lerp(1f, 0.86f, t);
            float x = Random.Range(-0.03f, 0.03f) * size.x;
            float y = Random.Range(-0.10f, 0.10f) * size.y;
            float bandAlpha = alpha * Mathf.Lerp(1f, 0.62f, t) * Random.Range(0.76f, 1f);

            Box(rt, "Band_" + i, WithAlpha(Bone, bandAlpha),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, y), new Vector2(width, height));
        }

        Random.state = previous;
        return root;
    }

    /// <summary>
    /// A bone-outlined control shell: an outer frame with an ink inset, so the result is a crisp
    /// hairline box in the idiom of the reference's bordered rows and key glyphs. Returns the
    /// frame, which carries the clickable graphic.
    /// </summary>
    public static GameObject Outline(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        float border = 2f, float frameAlpha = 0.85f, bool raycastTarget = false)
    {
        GameObject frame = Box(parent, name, WithAlpha(Bone, frameAlpha), anchorMin, anchorMax, pivot,
            anchoredPos, sizeDelta, raycastTarget);

        Box(frame.transform, "Inset", PanelDeep, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-border * 2f, -border * 2f));

        return frame;
    }

    /// <summary>
    /// A small square key glyph, matching the button prompts that close every reference layout.
    /// </summary>
    public static GameObject KeyGlyph(Transform parent, string glyph, Font font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, float glyphSize = 30f)
    {
        GameObject box = Outline(parent, "Key_" + glyph, anchorMin, anchorMax, pivot, anchoredPos,
            new Vector2(glyphSize, glyphSize), 2f, 0.92f);

        // Sized to fit inside the frame rather than to fill it: a three-character prompt such as
        // "LMB" is the widest thing this box ever has to hold, and at anything above half the box
        // it would spill over the border it is drawn inside.
        Type(box.transform, "Glyph", glyph, font, Mathf.RoundToInt(glyphSize * 0.46f),
            TextAnchor.MiddleCenter, Bone, FontStyle.Bold,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);

        return box;
    }

    /// <summary>
    /// The chrome bar across the foot of every screen: a key glyph, the action it performs, and a
    /// quiet note on the right.
    /// </summary>
    public static GameObject HintBar(Transform parent, Font font, string glyph, string action,
        string note, float height = 60f)
    {
        GameObject bar = Box(parent, "HintBar", Ink,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, height));

        Hairline(bar.transform, "TopRule",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, 2f));

        KeyGlyph(bar.transform, glyph, font,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(34f, 0f));

        Type(bar.transform, "Action", Spread(action), font, 20, TextAnchor.MiddleLeft, Cream,
            FontStyle.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(78f, 0f), new Vector2(460f, 28f), false);

        Type(bar.transform, "Note", note, font, 17, TextAnchor.MiddleRight, Faint, FontStyle.Normal,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-34f, 0f), new Vector2(900f, 26f), false);

        return bar;
    }

    /// <summary>
    /// A stacked menu row. The selected row carries the torn cream stroke with ink type on it;
    /// every other row is cream type on a barely-there ink band that lifts on hover.
    /// </summary>
    public static Button MenuItem(Transform parent, string name, string label, Font font, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        bool selected, float inset, int seed, UnityAction onClick)
    {
        Color band = selected ? new Color(0f, 0f, 0f, 0f) : WithAlpha(Ink, 0.42f);

        GameObject go = Box(parent, name, band, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta, true);

        Image image = go.GetComponent<Image>();
        image.sprite = RushHourSceneBuilder.GetUiSprite();
        image.type = Image.Type.Sliced;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = TintBlock(selected);

        // Added before the label, so the type always draws on top of the brush.
        if (selected)
        {
            Stroke(go.transform, "Stroke", new Vector2(sizeDelta.x + 46f, sizeDelta.y - 4f), seed);
        }

        Type(go.transform, "Label", label, font, fontSize, TextAnchor.MiddleLeft,
            selected ? Ink : Cream, FontStyle.Bold,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-inset * 2f, -8f), !selected);

        if (onClick != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return button;
    }

    /// <summary>
    /// A bordered control: bone frame, ink inset, centred label. The reference's button idiom.
    /// </summary>
    public static Button FramedButton(Transform parent, string name, string label, Font font,
        int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos,
        Vector2 sizeDelta, Color frameTint, Color labelColor, UnityAction onClick)
    {
        GameObject go = Outline(parent, name, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta,
            2f, 0.85f, true);

        Image image = go.GetComponent<Image>();
        image.sprite = RushHourSceneBuilder.GetUiSprite();
        image.type = Image.Type.Sliced;
        image.color = frameTint;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = TintBlock(false);

        Type(go.transform, "Label", Spread(label), font, fontSize, TextAnchor.MiddleCenter, labelColor,
            FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-12f, -8f), false);

        if (onClick != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return button;
    }

    /// <summary>
    /// The hover/press language for every control: the graphic carries the colour, so the tint
    /// block only has to brighten and dim it.
    /// </summary>
    private static ColorBlock TintBlock(bool selected)
    {
        ColorBlock colors = new ColorBlock();
        colors.normalColor = Color.white;
        colors.highlightedColor = selected ? new Color(0.98f, 0.94f, 0.86f) : new Color(1f, 0.90f, 0.74f);
        colors.pressedColor = new Color(0.80f, 0.66f, 0.52f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }
}
#endif
