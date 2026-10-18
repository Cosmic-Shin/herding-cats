using UnityEngine;

// Procedural white sprites (tint them with SpriteRenderer.color) so weapons work without art.
// Each is 1 world unit across; scale the transform for size.
public static class Shapes
{
    private static Sprite square, star, ring, crescent;

    public static Sprite Circle => ExplosionFx.GetCircle();

    // pivot at the left edge so a scaled square works as a beam/line from its origin
    public static Sprite Square => square != null ? square : square = Make(4, (x, y, n) => true, new Vector2(0f, 0.5f));

    public static Sprite Star => star != null ? star : star = Make(32, (x, y, n) =>
    {
        // 4-point star: inside if |dx|*|dy| small (a plus) or close to center
        float dx = Mathf.Abs(x + 0.5f - n / 2f) / (n / 2f), dy = Mathf.Abs(y + 0.5f - n / 2f) / (n / 2f);
        return dx * dy < 0.06f && dx + dy < 1f || dx * dx + dy * dy < 0.08f;
    });

    // 128px so big pulse rings (novas, halo, quakes) don't turn into chunky blocks
    public static Sprite Ring => ring != null ? ring : ring = Make(128, (x, y, n) =>
    {
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
        return d <= 1f && d >= 0.9f;
    });

    public static Sprite Crescent => crescent != null ? crescent : crescent = Make(32, (x, y, n) =>
    {
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        bool inOuter = Vector2.Distance(p, new Vector2(n / 2f, n / 2f)) <= n / 2f;
        bool inCut = Vector2.Distance(p, new Vector2(n * 0.7f, n * 0.62f)) <= n * 0.42f;
        return inOuter && !inCut;
    });

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> art = new System.Collections.Generic.Dictionary<string, Sprite>();

    // Pixel art from Resources (e.g. "landmine", "Icons/shuriken") as a sprite 1 unit across like the shapes
    // above, so it drops in at the same scale. Falls back to Circle if the file is missing.
    public static Sprite Art(string path)
    {
        if (art.TryGetValue(path, out Sprite s) && s != null) return s;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return Circle;
        tex.filterMode = FilterMode.Point;
        RectInt r = tex.isReadable ? OpaqueBounds(tex) : new RectInt(0, 0, tex.width, tex.height); // readable: trim empty canvas
        return art[path] = Sprite.Create(tex, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f), Mathf.Max(r.width, r.height));
    }

    private static RectInt OpaqueBounds(Texture2D tex)
    {
        Color32[] p = tex.GetPixels32();
        int minX = tex.width, minY = tex.height, maxX = -1, maxY = -1;
        for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
                if (p[y * tex.width + x].a > 8)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        return maxX < 0 ? new RectInt(0, 0, tex.width, tex.height) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static Sprite Make(int n, System.Func<int, int, int, bool> inside, Vector2? pivot = null)
    {
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                tex.SetPixel(x, y, inside(x, y, n) ? Color.white : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), pivot ?? new Vector2(0.5f, 0.5f), n);
    }
}
