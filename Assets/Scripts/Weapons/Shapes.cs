using UnityEngine;

public static class Shapes
{
    private static Sprite square, star, ring, crescent, plus, riot, bubble;

    // Curved riot shield, long side along x, bulging toward -y. Full colour (don't tint).
    public static Sprite RiotShield => riot != null ? riot : riot = MakeColored(24, 12, 30f, (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(12f, 30f));
        if (d > 28f || d < 23.2f) return Color.clear;
        if (d > 27.2f || d < 24f || x == 0 || x == 23) return new Color(0.13f, 0.15f, 0.25f);   // outline
        if (Mathf.Abs(x + 0.5f - 12f) < 2.2f && d > 24.6f && d < 26.8f) return new Color(1f, 0.8f, 0.3f); // gold boss
        if (d > 26.5f) return new Color(0.92f, 0.96f, 1f);                                           // rim highlight
        return x < 12 ? new Color(0.62f, 0.69f, 0.82f) : new Color(0.5f, 0.56f, 0.7f);               // two-tone steel
    });

    // Soap bubble: bright rim, faint body, specular glint top-left. White, tint it.
    public static Sprite Bubble => bubble != null ? bubble : bubble = MakeColored(48, 48, 48f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 24f, y + 0.5f - 24f) / 24f;
        float d = p.magnitude;
        if (d > 1f) return Color.clear;
        float a = 0.12f + Mathf.Pow(Mathf.InverseLerp(0.7f, 1f, d), 2f) * 0.75f;
        if (d > 0.93f) a = 0.95f;
        if ((p - new Vector2(-0.42f, 0.45f)).magnitude < 0.16f) a = 1f;                              // glint
        if (d > 0.72f && d < 0.82f && p.x > 0.25f && p.y < -0.25f) a = Mathf.Max(a, 0.6f);          // lower-right reflection
        return new Color(1f, 1f, 1f, a);
    });

    private static readonly Color Ink = new Color(0.06f, 0.05f, 0.1f);
    private static Sprite glob, fishBone, hairball, fish;

    // Enemy spit / boss bullet: outlined shaded ball. Light grey so a tint shows through; the glint stays bright.
    public static Sprite Glob => glob != null ? glob : glob = MakeOutlined(12, 12, 12f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 6f, y + 0.5f - 6f);
        if (p.magnitude > 4.9f) return Color.clear;
        if ((p - new Vector2(-1.6f, 1.6f)).magnitude < 1.2f) return Color.white;
        return p.x - p.y > 3.2f ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.85f, 0.85f, 0.85f);
    });

    // Sniper shot: a fish skeleton, head first along +x.
    public static Sprite FishBone => fishBone != null ? fishBone : fishBone = MakeOutlined(16, 9, 16f, (x, y) =>
    {
        Color bone = new Color(0.95f, 0.92f, 0.82f);
        if (x >= 11 && Mathf.Abs(y - 4) <= (15 - x) * 0.75f + 0.2f) return x == 13 && y == 5 ? Ink : bone; // head + eye
        if (y == 4 && x >= 2 && x <= 11) return bone;                                                    // spine
        if ((x == 4 || x == 6 || x == 8 || x == 10) && y >= 2 && y <= 6) return bone;                     // ribs
        if (x <= 2 && Mathf.Abs(y - 4) == 2 - x) return bone;                                            // tail fork
        return Color.clear;
    });

    // Lobbed hairball: matted grey-brown ball with darker strands.
    public static Sprite Hairball => hairball != null ? hairball : hairball = MakeOutlined(12, 12, 12f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 6f, y + 0.5f - 6f);
        if (p.magnitude > 4.9f) return Color.clear;
        if ((p - new Vector2(-1.8f, 1.8f)).magnitude < 1f) return new Color(0.85f, 0.78f, 0.72f);
        return Mathf.Sin(x * 1.4f + y * 0.7f) > 0.6f || Mathf.Sin(y * 1.7f - x * 0.5f) > 0.85f ? new Color(0.42f, 0.34f, 0.3f) : new Color(0.66f, 0.56f, 0.5f);
    });

    // XP: a little fish treat facing right. White-ish so the tier colour tints it.
    public static Sprite Fish => fish != null ? fish : fish = MakeOutlined(14, 10, 12f, (x, y) =>
    {
        Vector2 p = new Vector2((x + 0.5f - 8f) / 4.6f, (y + 0.5f - 5f) / 3f);
        bool body = p.sqrMagnitude <= 1f;
        bool tail = x <= 3 && Mathf.Abs(y + 0.5f - 5f) <= (4 - x) * 0.9f + 0.6f && x >= 1;
        if (!body && !tail) return Color.clear;
        if (x == 10 && y == 6) return Ink;                                        // eye
        if (body && (p - new Vector2(-0.2f, 0.45f)).magnitude < 0.3f) return Color.white; // shine
        return y <= 3 ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.9f, 0.9f, 0.9f);  // shaded belly
    });

    private static Sprite sack, shrineStone;

    // Shrine pedestal: a small mossy stone pillar with a glowing rune slot.
    public static Sprite ShrineStone => shrineStone != null ? shrineStone : shrineStone = MakeOutlined(12, 14, 14f, (x, y) =>
    {
        bool cap = y >= 11 && x >= 1 && x <= 10;
        bool column = y >= 2 && y < 11 && x >= 3 && x <= 8;
        bool foot = y < 2 && x >= 1 && x <= 10;
        if (!cap && !column && !foot) return Color.clear;
        if (column && y >= 5 && y <= 7 && x >= 5 && x <= 6) return new Color(1f, 0.95f, 0.75f); // rune slot
        if ((cap || foot) && (x + y) % 5 == 0) return new Color(0.42f, 0.62f, 0.35f);       // moss
        return x <= 4 || y == 12 ? new Color(0.72f, 0.72f, 0.78f) : new Color(0.52f, 0.52f, 0.6f);
    });

    // Gem thief marker: a loot sack with a gold coin poking out.
    public static Sprite Sack => sack != null ? sack : sack = MakeOutlined(10, 10, 16f, (x, y) =>
    {
        if (y == 9 && x == 6) return new Color(1f, 0.85f, 0.3f);
        if (y == 8 && x >= 3 && x <= 6) return x == 6 ? new Color(1f, 0.85f, 0.3f) : new Color(0.66f, 0.47f, 0.28f);
        if (y == 7 && x >= 4 && x <= 5) return new Color(0.35f, 0.22f, 0.12f);           // tie
        Vector2 p = new Vector2((x + 0.5f - 5f) / 4.4f, (y + 0.5f - 3.4f) / 3.5f);
        if (p.sqrMagnitude > 1f) return Color.clear;
        return p.x < -0.3f && p.y > 0f ? new Color(0.8f, 0.6f, 0.38f) : new Color(0.6f, 0.42f, 0.25f);
    });

    private static Sprite MakeOutlined(int w, int h, float ppu, System.Func<int, int, Color> pixel)
    {
        var fill = new Color[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) fill[x, y] = pixel(x, y);
        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && fill[x, y].a > 0f;
        return MakeColored(w + 2, h + 2, ppu, (x, y) =>
        {
            int fx = x - 1, fy = y - 1;
            if (Solid(fx, fy)) return fill[fx, fy];
            return Solid(fx - 1, fy) || Solid(fx + 1, fy) || Solid(fx, fy - 1) || Solid(fx, fy + 1) ? Ink : Color.clear;
        });
    }

    public static Sprite Plus => plus != null ? plus : plus = Make(7, (x, y, n) => (x >= 2 && x <= 4) || (y >= 2 && y <= 4));

    public static Sprite Circle => ExplosionFx.GetCircle();

    public static Sprite Square => square != null ? square : square = Make(4, (x, y, n) => true, new Vector2(0f, 0.5f));

    public static Sprite Star => star != null ? star : star = Make(32, (x, y, n) =>
    {
        float dx = Mathf.Abs(x + 0.5f - n / 2f) / (n / 2f), dy = Mathf.Abs(y + 0.5f - n / 2f) / (n / 2f);
        return dx * dy < 0.06f && dx + dy < 1f || dx * dx + dy * dy < 0.08f;
    });

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

    public static Sprite Art(string path)
    {
        if (art.TryGetValue(path, out Sprite s) && s != null) return s;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return Circle;
        tex.filterMode = FilterMode.Point;
        RectInt r = tex.isReadable ? OpaqueBounds(tex) : new RectInt(0, 0, tex.width, tex.height);
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

    private static Sprite MakeColored(int w, int h, float ppu, System.Func<int, int, Color> pixel)
    {
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    private static Sprite Make(int n,System.Func<int, int, int, bool> inside, Vector2? pivot = null)
    {
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                tex.SetPixel(x, y, inside(x, y, n) ? Color.white : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), pivot ?? new Vector2(0.5f, 0.5f), n);
    }
}
