using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OffscreenArrows : MonoBehaviour
{
    private const float EdgeInset = 44f;

    private static OffscreenArrows instance;
    private readonly List<(Transform target, Color color, Image arrow)> tracked = new List<(Transform, Color, Image)>();
    private RectTransform canvasRect;
    private Sprite arrowSprite;

    public static void Track(Transform target, Color color, Sprite icon = null)
    {
        if (target == null) return;
        if (instance == null) instance = new GameObject("OffscreenArrows").AddComponent<OffscreenArrows>();
        Image arrow = instance.MakeArrow(color);
        if (icon != null)
        {
            var img = new GameObject("Icon").AddComponent<Image>();
            img.transform.SetParent(arrow.transform, false);
            img.sprite = icon;
            img.raycastTarget = false;
            img.rectTransform.anchoredPosition = new Vector2(-38f, 0f);
            img.rectTransform.sizeDelta = new Vector2(34f, 34f);
        }
        instance.tracked.Add((target, color, arrow));
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasRect = (RectTransform)transform;
        arrowSprite = MakeArrowSprite();
    }

    private Image MakeArrow(Color color)
    {
        var go = new GameObject("Arrow");
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.sprite = arrowSprite;
        img.color = color;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(44, 44);
        go.SetActive(false);
        return img;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector2 half = canvasRect.rect.size / 2f - Vector2.one * EdgeInset;
        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);

        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            (Transform target, Color color, Image arrow) = tracked[i];
            bool gone = target == null || !target.gameObject.activeInHierarchy
                        || (target.TryGetComponent(out EnemyHealth h) && h.IsDead);
            if (gone)
            {
                Destroy(arrow.gameObject);
                tracked.RemoveAt(i);
                continue;
            }

            Vector3 vp = cam.WorldToViewportPoint(target.position);
            bool onScreen = vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
            arrow.gameObject.SetActive(!onScreen);
            if (onScreen) continue;

            Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f) * canvasRect.rect.size;
            float t = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(d.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(d.y)));
            arrow.rectTransform.anchoredPosition = d * t;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            arrow.rectTransform.localScale = Vector3.one * pulse;
            if (arrow.transform.childCount > 0) arrow.transform.GetChild(0).rotation = Quaternion.identity;
        }
    }

    private static Sprite MakeArrowSprite()
    {
        const int n = 16;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point };
        bool Inside(int x, int y) => x >= 2 && x <= 13 && Mathf.Abs(y - 7.5f) <= (13 - x) * 0.55f + 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool inside = Inside(x, y);
                bool edge = !inside && (Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1));
                tex.SetPixel(x, y, inside ? Color.white : edge ? new Color(0.08f, 0.05f, 0.12f) : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
