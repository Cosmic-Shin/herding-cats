using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class RuntimeUI
{
    public static Canvas Canvas(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static Image Box(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    public static readonly Color ItemColor = new Color(0.85f, 0.88f, 0.96f);
    public static readonly Color BackdropColor = new Color(0.03f, 0.02f, 0.08f, 0.82f);

    public static Image Frame(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, bool gold = false)
    {
        Image img = Box(parent, name, anchorMin, anchorMax, gold ? new Color(0.14f, 0.1f, 0.04f, 0.85f) : BackdropColor);
        return img;
    }

    public static RectTransform Title(Transform parent, string text, float size, Vector2 pos, float width = 1400f)
    {
        var holder = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(parent, false);
        holder.anchoredPosition = pos;
        holder.sizeDelta = new Vector2(width, size * 1.25f);
        float px = size / 21f;
        TextMeshProUGUI Layer(Color color, Vector2 offset)
        {
            TextMeshProUGUI t = Text(holder, "", size, color, offset, holder.sizeDelta);
            t.characterSpacing = 4f;
            return t;
        }
        Layer(new Color(0.02f, 0.02f, 0.06f, 0.5f), new Vector2(1.5f, -2.3f) * px);
        Color ink = new Color(0.05f, 0.04f, 0.12f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Layer(ink, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * px);
        }
        TextMeshProUGUI main = Layer(Color.white, Vector2.zero);
        main.enableVertexGradient = true;
        main.colorGradient = new VertexGradient(Color.white, Color.white, new Color(1f, 0.93f, 0.8f), new Color(1f, 0.93f, 0.8f));
        SetTitle(holder, text);
        return holder;
    }

    public static void SetTitle(RectTransform holder, string text)
    {
        TextMeshProUGUI[] layers = holder.GetComponentsInChildren<TextMeshProUGUI>();
        string plain = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
        for (int i = 0; i < layers.Length; i++) layers[i].text = i == layers.Length - 1 ? text : plain;
    }

    // Fixed arrow/d-pad routes (null = nowhere), for rows where left/right changes a value or automatic nav jumps oddly.
    public static void Nav(Selectable s, Selectable up = null, Selectable down = null, Selectable left = null, Selectable right = null) =>
        s.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };

    public static Button TextButton(Transform parent, string label, Vector2 pos, float size, UnityAction onClick, float width = 0f)
    {
        TextMeshProUGUI t = Outline(Text(parent, label.ToUpper(), size, Color.white, pos, new Vector2(width > 0f ? width : size * 11f, size * 1.45f)));
        t.name = label + "Button";
        t.raycastTarget = true;
        var b = t.gameObject.AddComponent<Button>();
        b.targetGraphic = t;
        b.transition = Selectable.Transition.ColorTint;
        ColorBlock c = b.colors;
        c.normalColor = ItemColor;
        c.highlightedColor = c.selectedColor = Gold;
        c.pressedColor = new Color(1f, 0.97f, 0.75f);
        c.disabledColor = new Color(0.45f, 0.47f, 0.58f);
        c.fadeDuration = 0.06f;
        b.colors = c;
        t.gameObject.AddComponent<CardHover>();
        b.onClick.AddListener(() => Sfx.Play(Sfx.Id.Click, 0.7f));
        b.onClick.AddListener(onClick);
        return b;
    }

    public static TextMeshProUGUI Outline(TextMeshProUGUI t, float width = 0.2f)
    {
        // Mobile SDF shader only draws outlines with OUTLINE_ON; dilate so thin pixel strokes survive (matches "PixelFont Outline.mat").
        t.fontMaterial.EnableKeyword("OUTLINE_ON");
        t.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        t.outlineWidth = width;
        t.outlineColor = new Color32(10, 8, 24, 255);
        return t;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
                                       TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        return t;
    }

    public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, UnityAction onClick) =>
        TextButton(parent, label, pos, Mathf.Min(size.y * 0.5f, 30f), onClick, size.x);

    public static void Restyle(Transform root)
    {
        bool wasActive = root.gameObject.activeSelf;
        root.gameObject.SetActive(true);
        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null || (img.sprite.name != "box" && img.sprite.name != "box_gold")) continue;
            img.sprite = null;
            if (!img.TryGetComponent(out Button b))
            {
                img.color = BackdropColor;
                foreach (Shadow fx in img.GetComponents<Shadow>()) fx.enabled = false;
                continue;
            }
            bool card = img.transform.Find("Icon") != null || img.GetComponentsInChildren<Image>(true).Length > 2;
            if (card) { img.color = Opaque(BackdropColor); b.transition = Selectable.Transition.None; continue; }

            img.color = Color.clear;
            TextMeshProUGUI label = img.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) continue;
            label.text = label.text.ToUpper();
            label.color = Color.white;
            Outline(label);
            b.targetGraphic = label;
            b.transition = Selectable.Transition.ColorTint;
            ColorBlock c = b.colors;
            c.normalColor = ItemColor;
            c.highlightedColor = c.selectedColor = Gold;
            c.pressedColor = new Color(1f, 0.97f, 0.75f);
            c.disabledColor = new Color(0.45f, 0.47f, 0.58f);
            c.fadeDuration = 0.06f;
            b.colors = c;
            if (!img.TryGetComponent(out CardHover _)) img.gameObject.AddComponent<CardHover>();
        }
        root.gameObject.SetActive(wasActive);
    }

    public static Color Opaque(Color c) { c.a = 1f; return c; }

    public static RectTransform ReplaceTitle(TextMeshProUGUI old, string text, float size)
    {
        RectTransform t = Title(old.transform.parent, text, size, old.rectTransform.anchoredPosition, old.rectTransform.sizeDelta.x + 200f);
        t.anchorMin = old.rectTransform.anchorMin;
        t.anchorMax = old.rectTransform.anchorMax;
        t.anchoredPosition = old.rectTransform.anchoredPosition;
        old.gameObject.SetActive(false);
        return t;
    }
}

public class SelectGlow : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
{
    private Outline rim;

    private void Awake()
    {
        rim = gameObject.AddComponent<Outline>();
        rim.effectColor = RuntimeUI.Gold;
        rim.effectDistance = new Vector2(3f, -3f);
        rim.enabled = false;
    }

    public void OnSelect(UnityEngine.EventSystems.BaseEventData e) => rim.enabled = true;
    public void OnDeselect(UnityEngine.EventSystems.BaseEventData e) => rim.enabled = false;
}
