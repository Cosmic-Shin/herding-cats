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

    public enum CatSkin { None, Shield, Sniper, Toxic, Charger, Splitter, Kamikaze, Thief, Healer, Mama }

    private static readonly Dictionary<(Sprite, CatSkin), Sprite> skinned = new Dictionary<(Sprite, CatSkin), Sprite>();

    private static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }

    private static readonly Dictionary<CatSkin, Color[]> Ramps = new Dictionary<CatSkin, Color[]>
    {
        { CatSkin.Shield,   new[] { Hex("4A5570"), Hex("8A96B0"), Hex("C8D2E6") } },
        { CatSkin.Sniper,   new[] { Hex("2C2340"), Hex("5A4A7A"), Hex("8E7CB8") } },
        { CatSkin.Toxic,    new[] { Hex("2E6B2A"), Hex("6CC24A"), Hex("B6F06A") } },
        { CatSkin.Charger,  new[] { Hex("7A2418"), Hex("C8492E"), Hex("F08A5A") } },
        { CatSkin.Splitter, new[] { Hex("5A4E8A"), Hex("9A86D0"), Hex("D6CCF5") } },
        { CatSkin.Kamikaze, new[] { Hex("6A3A18"), Hex("C0702E"), Hex("F0B060") } },
        { CatSkin.Thief,    new[] { Hex("3E3A44"), Hex("6E6878"), Hex("A8A2B4") } },
        { CatSkin.Healer,   new[] { Hex("B07090"), Hex("F0B0C8"), Hex("FFF0F6") } },
        { CatSkin.Mama,     new[] { Hex("A0704A"), Hex("E0B080"), Hex("FFF0D8") } },
    };

    public static Color SkinColor(CatSkin skin) => Ramps.TryGetValue(skin, out Color[] r) ? r[1] : Color.white;

    public static Sprite Skinned(Sprite s, CatSkin skin)
    {
        if (s == null || skin == CatSkin.None || !s.texture.isReadable) return s;
        if (skinned.TryGetValue((s, skin), out Sprite k) && k != null) return k;

        Rect r = s.textureRect;
        int w = (int)r.width, h = (int)r.height;
        Color[] p = s.texture.GetPixels((int)r.x, (int)r.y, w, h);
        Color[] ramp = Ramps[skin];

        int x0 = w, x1 = -1, y0 = h, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = p[y * w + x];
                if (c.a < 0.5f) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                Color.RGBToHSV(c, out _, out float sat, out float val);
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                if (lum < 0.3f || (sat > 0.3f && val > 0.5f)) continue;
                float t = Mathf.InverseLerp(0.3f, 1f, lum);
                Color n = t < 0.6f ? Color.Lerp(ramp[0], ramp[1], t / 0.6f) : Color.Lerp(ramp[1], ramp[2], (t - 0.6f) / 0.4f);
                n.a = c.a;
                p[y * w + x] = n;
            }

        if (x1 >= 0) Accessory(p, w, h, skin, (x0 + x1) / 2, y0, y1);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(p);
        tex.Apply(false, false);
        return skinned[(s, skin)] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(s.pivot.x / w, s.pivot.y / h), s.pixelsPerUnit);
    }

    private static void Accessory(Color[] p, int w, int h, CatSkin skin, int cx, int bottom, int top)
    {
        void P(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < w && y < h) p[y * w + x] = c; }
        void Box(int xa, int ya, int xb, int yb, Color c) { for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) P(x, y, c); }
        Color ink = new Color(0.08f, 0.05f, 0.12f);
        int eye = bottom + (top - bottom) * 45 / 100;
        int dip = top - 3;

        switch (skin)
        {
            case CatSkin.Shield:
                Box(cx - 5, dip - 2, cx + 5, dip + 1, Hex("C8D2E6")); Box(cx - 5, dip - 2, cx + 5, dip - 2, Hex("4A5570"));
                Box(cx - 4, dip + 1, cx + 4, dip + 1, Hex("EEF3FF")); Box(cx - 1, dip + 2, cx, dip + 4, Hex("E8402E"));
                break;
            case CatSkin.Sniper:
                Box(cx - 4, dip, cx + 3, dip + 1, Hex("3A5A30")); Box(cx - 3, dip + 2, cx + 2, dip + 2, Hex("3A5A30")); P(cx, dip + 3, Hex("3A5A30"));
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (dx != 0 || dy != 0) P(cx + 4 + dx, eye - 2 + dy, Hex("E8402E"));
                P(cx + 4, eye - 2, Hex("FFD940"));
                break;
            case CatSkin.Toxic:
                foreach (var (dx, dy) in new[] { (-6, -3), (4, -5), (-2, 2), (5, 1) }) Box(cx + dx, eye + dy, cx + dx + 1, eye + dy + 1, Hex("7A3AA0"));
                break;
            case CatSkin.Charger:
                foreach (int side in new[] { -1, 1 })
                {
                    P(cx + side * 3, dip + 1, Hex("F0E6D0")); P(cx + side * 4, dip + 1, Hex("F0E6D0"));
                    P(cx + side * 4, dip + 2, Hex("F0E6D0")); P(cx + side * 5, dip + 3, Hex("D8CCB0"));
                }
                break;
            case CatSkin.Splitter:
                for (int y = bottom + 2; y <= dip; y++) { P(cx, y, ink); if ((y - bottom) % 2 == 0) { P(cx - 1, y, ink); P(cx + 1, y, ink); } }
                break;
            case CatSkin.Kamikaze:
                Box(cx - 2, dip, cx + 2, dip + 3, Hex("38384D")); Box(cx - 1, dip + 4, cx + 1, dip + 4, Hex("38384D")); P(cx - 1, dip + 3, Hex("8A8AA0"));
                P(cx + 1, dip + 5, Hex("996133")); P(cx + 2, dip + 6, Hex("FFD940"));
                break;
            case CatSkin.Thief:
                Box(cx - 7, eye - 1, cx + 7, eye + 1, ink); P(cx - 8, eye + 1, ink); P(cx + 8, eye + 1, ink);
                P(cx - 3, eye, Color.white); P(cx + 3, eye, Color.white);
                break;
            case CatSkin.Healer:
                Box(cx - 3, dip, cx + 3, dip + 3, Color.white); Box(cx - 3, dip, cx + 3, dip, Hex("D8DEF0"));
                Box(cx - 1, dip + 2, cx + 1, dip + 2, Hex("E8402E")); P(cx, dip + 1, Hex("E8402E")); P(cx, dip + 3, Hex("E8402E"));
                break;
            case CatSkin.Mama:
                Box(cx - 4, dip, cx - 2, dip + 2, Hex("FF7AD9")); Box(cx + 2, dip, cx + 4, dip + 2, Hex("FF7AD9"));
                Box(cx - 1, dip + 1, cx + 1, dip + 1, Hex("E040A0")); P(cx - 3, dip + 2, Hex("FFB0E8")); P(cx + 3, dip + 2, Hex("FFB0E8"));
                break;
        }
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
