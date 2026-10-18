using System.Collections.Generic;
using UnityEngine;

public static class SpriteFx
{
    private static readonly Color OutlineColor = new Color(0.08f, 0.05f, 0.12f, 1f);
    private static readonly Dictionary<Sprite, Sprite> outlined = new Dictionary<Sprite, Sprite>();
    private static readonly Dictionary<Sprite, Sprite> flashed = new Dictionary<Sprite, Sprite>();

    public static Sprite Outlined(Sprite s)
    {
        if (s == null) return null;
        if (!outlined.TryGetValue(s, out Sprite o) || o == null) outlined[s] = o = Bake(s, 1, false);
        return o;
    }

    public static Sprite Flash(Sprite s)
    {
        if (s == null) return null;
        if (!flashed.TryGetValue(s, out Sprite f) || f == null) flashed[s] = f = Bake(Outlined(s), 0, true);
        return f;
    }

    private static Sprite Bake(Sprite s, int pad, bool white)
    {
        Texture2D src = s.texture;
        if (!src.isReadable) return s;

        Rect r = s.textureRect;
        int w = (int)r.width, h = (int)r.height, W = w + pad * 2, H = h + pad * 2;
        Color[] p = src.GetPixels((int)r.x, (int)r.y, w, h);
        float A(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? 0f : p[y * w + x].a;

        var o = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int sx = x - pad, sy = y - pad;
                float a = A(sx, sy);
                if (white) o[y * W + x] = a > 0.01f ? new Color(1f, 1f, 1f, a) : Color.clear;
                else if (a > 0.5f) o[y * W + x] = p[sy * w + sx];
                else if (A(sx - 1, sy) > 0.5f || A(sx + 1, sy) > 0.5f || A(sx, sy - 1) > 0.5f || A(sx, sy + 1) > 0.5f) o[y * W + x] = OutlineColor;
                else o[y * W + x] = Color.clear;
            }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(o);
        tex.Apply(false, true);
        Vector2 pivot = new Vector2((s.pivot.x + pad) / W, (s.pivot.y + pad) / H);
        return Sprite.Create(tex, new Rect(0, 0, W, H), pivot, s.pixelsPerUnit);
    }

    public static SpriteRenderer AddShadow(SpriteRenderer sr, float width = 0.9f)
    {
        var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(sr.transform, false);
        shadow.sprite = Shapes.Circle;
        shadow.color = new Color(0f, 0f, 0f, 0.3f);
        shadow.sortingLayerID = sr.sortingLayerID;
        shadow.sortingOrder = sr.sortingOrder - 1;
        Bounds b = sr.sprite != null ? sr.sprite.bounds : new Bounds(Vector3.zero, Vector3.one * 0.5f);
        shadow.transform.localPosition = new Vector3(b.center.x, b.min.y + b.size.y * 0.03f, 0f);
        shadow.transform.localScale = new Vector3(b.size.x * width, b.size.x * width * 0.3f, 1f);
        return shadow;
    }
}
