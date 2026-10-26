using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Herding Cats" UI skin: plum jelly panels and gold for whatever is selected. Decoration is kept to
// one cat touch per screen (a cat on a panel, or ears on the picked hero). Sprites live in Resources/UI/Theme.
// Used by the HUD, level-up, pause, run select and game over. The main menu keeps the old look.
public static class CatTheme
{
    public enum Style { Panel, PanelGold, Slot, SlotGold, Pink, Slime, Dim }

    // Each sprite pixel is drawn this many UI units wide (same as UiIcons.Frame).
    public const float Px = 3f;

    public static readonly Color Ink = new Color32(20, 14, 32, 255);
    public static readonly Color Plum = new Color32(38, 27, 62, 255);
    public static readonly Color Lilac = new Color32(170, 142, 224, 255);
    public static readonly Color LilacHi = new Color32(222, 204, 255, 255);
    public static readonly Color Cream = new Color32(255, 244, 228, 255);
    public static readonly Color Faint = new Color32(150, 136, 186, 255);
    public static readonly Color Pink = new Color32(246, 118, 160, 255);
    public static readonly Color PinkHi = new Color32(255, 184, 208, 255);
    public static readonly Color Slime = new Color32(92, 192, 244, 255);
    public static readonly Color SlimeHi = new Color32(176, 232, 255, 255);
    public static readonly Color Gold = new Color32(255, 210, 74, 255);
    public static readonly Color GoldHi = new Color32(255, 240, 170, 255);
    public static readonly Color Mint = new Color32(124, 255, 154, 255);
    public static readonly Color BossPurple = new Color32(198, 84, 255, 255);
    public static readonly Color DimColor = new Color(0.055f, 0.03f, 0.11f, 0.86f);

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Load(string name)
    {
        if (!cache.TryGetValue(name, out Sprite s) || s == null) cache[name] = s = Resources.Load<Sprite>("UI/Theme/" + name);
        return s;
    }

    public static Sprite Get(Style style) => Load(style switch
    {
        Style.PanelGold => "panel_gold",
        Style.Slot => "slot",
        Style.SlotGold => "slot_gold",
        Style.Pink => "pink",
        Style.Slime => "slime",
        Style.Dim => "dim",
        _ => "panel",
    });

    // Turn an existing Image into a sliced theme frame.
    public static Image Frame(Image img, Style style)
    {
        if (img == null) return null;
        Sprite s = Get(style);
        if (s == null) return img; // sprites missing: leave the old look rather than a white box
        img.sprite = s;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / Px;
        img.color = Color.white;
        return img;
    }

    // Bars: a thin frame plus a shaded fill that is tinted per bar.
    public static void BarFrame(Image img)
    {
        Sprite s = Load("bar_frame");
        if (s == null) return;
        img.sprite = s;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / Px;
        img.color = Color.white;
    }

    public static void BarFill(Image img, Color tint)
    {
        Sprite s = Load("bar_fill");
        if (s != null)
        {
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / Px;
        }
        img.color = tint;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Vector2? anchor = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    // A new framed panel (centre-anchored).
    public static Image Panel(Transform parent, string name, Style style, Vector2 pos, Vector2 size)
    {
        Image img = Img(parent, name, null, pos, size);
        img.preserveAspect = false;
        Frame(img, style);
        return img;
    }

    // Two cat ears standing on the top corners of a panel (the picked hero card).
    public static void Ears(RectTransform panel, string sprite)
    {
        Sprite s = Load(sprite);
        if (s == null || panel.Find("EarL") != null) return;
        float size = 11f * Px, inset = 3f * Px, drop = 2f * Px;
        for (int side = 0; side < 2; side++)
        {
            bool right = side == 1;
            Image ear = Img(panel, right ? "EarR" : "EarL", s, Vector2.zero, new Vector2(size, size), new Vector2(right ? 1f : 0f, 1f));
            ear.rectTransform.pivot = new Vector2(0f, 0f);
            ear.rectTransform.anchoredPosition = new Vector2(right ? -inset : inset, -drop);
            if (right) ear.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            ear.preserveAspect = false;
        }
    }

    // Dark rounded plate placed just behind a title (as the sibling before it).
    public static Image PlateBehind(RectTransform title, Vector2 size)
    {
        Image plate = Img(title.parent, "TitlePlate", Load("plate"), title.anchoredPosition, size);
        plate.preserveAspect = false;
        plate.type = UnityEngine.UI.Image.Type.Sliced;
        plate.pixelsPerUnitMultiplier = 1f / Px;
        var rt = plate.rectTransform;
        rt.anchorMin = title.anchorMin;
        rt.anchorMax = title.anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        // Centre the plate on the title's centre, whatever the title's pivot.
        Vector2 centre = title.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - title.pivot, title.sizeDelta);
        rt.anchoredPosition = centre;
        plate.transform.SetSiblingIndex(title.GetSiblingIndex());
        return plate;
    }

    // A slime cat picture (a frame copied into Resources/UI/Theme so menus can show one).
    public static Image Cat(Transform parent, string which, Vector2 pos, float size, Vector2? anchor = null) =>
        Img(parent, "Cat", Load("cat_" + which), pos, new Vector2(size, size), anchor);

    // Theme a button: a jelly frame that turns gold while selected.
    // Works for scene buttons (they have their own Image) and RuntimeUI.TextButton (text only: a frame is added behind).
    public static void Button(Button b, float pad = 18f)
    {
        if (b == null || b.GetComponent<ThemeButton>() != null) return;
        var tb = b.gameObject.AddComponent<ThemeButton>();
        tb.button = b;
        if (b.TryGetComponent(out Image own))
        {
            tb.frame = Frame(own, Style.Panel);
            own.raycastTarget = true;
        }
        else
        {
            // Text-only button: the frame is a sibling just behind it that follows its rect.
            var rt = (RectTransform)b.transform;
            Image frame = Img(rt.parent, b.name + "Frame", Get(Style.Panel), Vector2.zero, Vector2.zero);
            frame.preserveAspect = false;
            Frame(frame, Style.Panel);
            frame.transform.SetSiblingIndex(rt.GetSiblingIndex());
            var follow = frame.gameObject.AddComponent<ThemeFollow>();
            follow.target = rt;
            follow.pad = pad;
            tb.frame = frame;
        }
    }

    public static bool IsSelected(GameObject go) => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == go;
}

// Keeps a themed button's frame gold while it is selected, grey when it can't be used.
public class ThemeButton : MonoBehaviour
{
    public Button button;
    public Image frame;

    private void LateUpdate()
    {
        if (frame == null || button == null) return;
        bool on = CatTheme.IsSelected(gameObject) && button.interactable;
        Sprite want = CatTheme.Get(on ? CatTheme.Style.PanelGold : CatTheme.Style.Panel);
        if (want != null && frame.sprite != want) frame.sprite = want;
        frame.color = button.interactable ? Color.white : new Color(0.6f, 0.6f, 0.7f, 0.85f);
    }
}

// Frame behind a text-only button: copies its rect every frame and hides with it.
public class ThemeFollow : MonoBehaviour
{
    public RectTransform target;
    public float pad;
    private Image img;

    private void Awake() => img = GetComponent<Image>();

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        var rt = (RectTransform)transform;
        rt.anchorMin = target.anchorMin;
        rt.anchorMax = target.anchorMax;
        rt.pivot = target.pivot;
        rt.anchoredPosition = target.anchoredPosition;
        rt.sizeDelta = target.sizeDelta + new Vector2(pad * 2f, pad * 0.6f);
        rt.localScale = target.localScale;
        rt.localRotation = target.localRotation;
        bool show = target.gameObject.activeSelf;
        if (img.enabled != show) img.enabled = show;
        foreach (Transform child in transform) child.gameObject.SetActive(show);
    }
}
