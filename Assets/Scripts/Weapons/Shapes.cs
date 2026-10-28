using UnityEngine;

public static class Shapes
{
    private static Sprite square, star, ring, crescent, plus, riot, bubble, softGlow, softRays;

    public static Sprite SoftRays
    {
        get
        {
            if (softRays != null) return softRays;
            const int n = 96;
            var tex = new Texture2D(n, n) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f) / (n / 2f);
                    float d = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 10f);
                    float fade = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ray * fade * fade));
                }
            tex.Apply();
            return softRays = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }

    public static Sprite SoftGlow
    {
        get
        {
            if (softGlow != null) return softGlow;
            const int n = 64;
            var tex = new Texture2D(n, n) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return softGlow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }

    public static Sprite RiotShield => riot != null ? riot : riot = MakeColored(24, 12, 30f, (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(12f, 30f));
        if (d > 28f || d < 23.2f) return Color.clear;
        if (d > 27.2f || d < 24f || x == 0 || x == 23) return new Color(0.13f, 0.15f, 0.25f);
        if (Mathf.Abs(x + 0.5f - 12f) < 2.2f && d > 24.6f && d < 26.8f) return new Color(1f, 0.8f, 0.3f);
        if (d > 26.5f) return new Color(0.92f, 0.96f, 1f);
        return x < 12 ? new Color(0.62f, 0.69f, 0.82f) : new Color(0.5f, 0.56f, 0.7f);
    });

    public static Sprite Bubble => bubble != null ? bubble : bubble = MakeColored(48, 48, 48f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 24f, y + 0.5f - 24f) / 24f;
        float d = p.magnitude;
        if (d > 1f) return Color.clear;
        float a = 0.12f + Mathf.Pow(Mathf.InverseLerp(0.7f, 1f, d), 2f) * 0.75f;
        if (d > 0.93f) a = 0.95f;
        if ((p - new Vector2(-0.42f, 0.45f)).magnitude < 0.16f) a = 1f;
        if (d > 0.72f && d < 0.82f && p.x > 0.25f && p.y < -0.25f) a = Mathf.Max(a, 0.6f);
        return new Color(1f, 1f, 1f, a);
    });

    private static readonly Color Ink = new Color(0.06f, 0.05f, 0.1f);
    private static Sprite glob, fishBone, hairball, fish;

    public static Sprite Glob => glob != null ? glob : glob = MakeOutlined(12, 12, 12f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 6f, y + 0.5f - 6f);
        if (p.magnitude > 4.9f) return Color.clear;
        if ((p - new Vector2(-1.6f, 1.6f)).magnitude < 1.2f) return Color.white;
        return p.x - p.y > 3.2f ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.85f, 0.85f, 0.85f);
    });

    public static Sprite FishBone => fishBone != null ? fishBone : fishBone = MakeOutlined(16, 9, 16f, (x, y) =>
    {
        Color bone = new Color(0.95f, 0.92f, 0.82f);
        if (x >= 11 && Mathf.Abs(y - 4) <= (15 - x) * 0.75f + 0.2f) return x == 13 && y == 5 ? Ink : bone;
        if (y == 4 && x >= 2 && x <= 11) return bone;
        if ((x == 4 || x == 6 || x == 8 || x == 10) && y >= 2 && y <= 6) return bone;
        if (x <= 2 && Mathf.Abs(y - 4) == 2 - x) return bone;
        return Color.clear;
    });

    public static Sprite Hairball => hairball != null ? hairball : hairball = MakeOutlined(12, 12, 12f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 6f, y + 0.5f - 6f);
        if (p.magnitude > 4.9f) return Color.clear;
        if ((p - new Vector2(-1.8f, 1.8f)).magnitude < 1f) return new Color(0.85f, 0.78f, 0.72f);
        return Mathf.Sin(x * 1.4f + y * 0.7f) > 0.6f || Mathf.Sin(y * 1.7f - x * 0.5f) > 0.85f ? new Color(0.42f, 0.34f, 0.3f) : new Color(0.66f, 0.56f, 0.5f);
    });

    public static Sprite Fish => fish != null ? fish : fish = MakeOutlined(14, 10, 12f, (x, y) =>
    {
        Vector2 p = new Vector2((x + 0.5f - 8f) / 4.6f, (y + 0.5f - 5f) / 3f);
        bool body = p.sqrMagnitude <= 1f;
        bool tail = x <= 3 && Mathf.Abs(y + 0.5f - 5f) <= (4 - x) * 0.9f + 0.6f && x >= 1;
        if (!body && !tail) return Color.clear;
        if (x == 10 && y == 6) return Ink;
        if (body && (p - new Vector2(-0.2f, 0.45f)).magnitude < 0.3f) return Color.white;
        return y <= 3 ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.9f, 0.9f, 0.9f);
    });

    private static Sprite orb;

    public static Sprite Orb => orb != null ? orb : orb = MakeOutlined(12, 12, 14f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 6f, y + 0.5f - 6f);
        float d = p.magnitude;
        if (d > 5.6f) return Color.clear;
        if ((p - new Vector2(-1.8f, 1.8f)).magnitude < 1.3f) return Color.white;
        if (d > 4.4f || p.x - p.y > 4.5f) return new Color(0.62f, 0.62f, 0.66f);
        return d < 2.6f ? new Color(0.97f, 0.97f, 0.97f) : new Color(0.84f, 0.84f, 0.86f);
    });

    private static Sprite frostVortex, laserDisc;

    public static Sprite FrostVortex => frostVortex != null ? frostVortex : frostVortex = MakeColored(128, 128, 128f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 64f, y + 0.5f - 64f) / 64f;
        float r = p.magnitude;
        if (r > 1f) return Color.clear;
        float arm = Mathf.Cos(3f * (Mathf.Atan2(p.y, p.x) + r * 4.5f));
        float v = Mathf.Lerp(arm, 1f, Mathf.Clamp01(1f - r * 2.4f));
        if (r > 0.93f) return v > 0.2f ? new Color(0.32f, 0.62f, 0.95f) : Color.clear;
        if (v < 0.2f) return new Color(0.12f, 0.3f, 0.6f, 0.5f);
        return v > 0.8f ? new Color(0.94f, 0.99f, 1f) : v > 0.5f ? new Color(0.62f, 0.9f, 1f) : new Color(0.32f, 0.62f, 0.95f);
    });

    public static Sprite LaserDisc => laserDisc != null ? laserDisc : laserDisc = MakeOutlined(16, 16, 16f, (x, y) =>
    {
        Vector2 p = new Vector2(x + 0.5f - 8f, y + 0.5f - 8f);
        float d = p.magnitude;
        if (d > 7.6f || d < 1.5f) return Color.clear;
        if (d < 2.8f) return new Color(0.86f, 0.86f, 0.92f);
        if (d > 6.6f) return new Color(0.72f, 0.14f, 0.5f);
        if (Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y, p.x) + 0.8f)) > 0.94f) return Color.white;
        return d < 4.7f ? new Color(1f, 0.6f, 0.88f) : new Color(1f, 0.36f, 0.74f);
    });

    private static Sprite sack;

    private static Sprite pawPrint, statueRune, paverRune;

    private static readonly string[] StatueRows =
    {
        "..DD........DD..",
        "..DLD......DSD..",
        "..DLLD....DSSD..",
        "..DLLLDDDDSSSD..",
        ".DLLLLLLLLSSSSD.",
        ".DLLrrLLLLrrSSD.",
        ".DLLLLLDDLLLSSD.",
        ".DLLLLLLLLLSSSD.",
        "..DLLLLLLLLSSD..",
        "...DDLLLLLLDD...",
        "..DLLLLLLLSSSD..",
        ".DLLLLLrrLSSSSD.",
        ".DLLLLrrrrSSSSD.",
        ".DLLLLLrrLSSSSDD",
        ".DLLLLLLLLSSSSDS",
        ".DLLDLLDDLLDSSDD",
        ".DDDDDDDDDDDDDD.",
        "DLLLLLLLLLLLLSSD",
        "DSSMSSSSSSSSMSSD",
        "DDDDDDDDDDDDDDDD",
    };
    private static readonly int[] StatueSnow = { 1, 2, 3, 4, 17 };

    private static readonly string[] PaverRows = { ".LLLL.", "LrrrrS", ".SSSD." };
    private static readonly int[] PaverSnow = { 0 };

    private static readonly Sprite[] statues = new Sprite[3], pavers = new Sprite[3];

    public static Sprite ShrineStatue(int biome) => StoneArt(statues, StatueRows, StatueSnow, biome);
    public static Sprite ShrinePaver(int biome) => StoneArt(pavers, PaverRows, PaverSnow, biome);
    public static Sprite ShrineStatueRune => statueRune != null ? statueRune : statueRune = RuneOf(StatueRows);
    public static Sprite ShrinePaverRune => paverRune != null ? paverRune : paverRune = RuneOf(PaverRows);

    private static char Px(string[] rows, int x, int y) => rows[rows.Length - 1 - y][x];

    private static Sprite StoneArt(Sprite[] cache, string[] rows, int[] snowRows, int biome)
    {
        biome = Mathf.Clamp(biome, 0, 2);
        if (cache[biome] != null) return cache[biome];
        Color light = biome == 2 ? new Color(0.38f, 0.32f, 0.34f) : new Color(0.74f, 0.73f, 0.8f);
        Color stone = biome == 2 ? new Color(0.25f, 0.21f, 0.24f) : new Color(0.53f, 0.52f, 0.62f);
        Color dark = biome == 2 ? new Color(0.13f, 0.1f, 0.12f) : new Color(0.3f, 0.28f, 0.38f);
        Color moss = biome == 2 ? new Color(1f, 0.5f, 0.15f) : biome == 1 ? new Color(0.92f, 0.96f, 1f) : new Color(0.42f, 0.66f, 0.38f);
        int h = rows.Length;
        return cache[biome] = MakeOutlined(rows[0].Length, h, 14f, (x, y) =>
        {
            char c = Px(rows, x, y);
            int fromTop = h - 1 - y;
            if (biome == 1 && (c == 'L' || c == 'S') && System.Array.IndexOf(snowRows, fromTop) >= 0 && (x + fromTop) % 4 != 0)
                return new Color(0.94f, 0.97f, 1f);
            return c switch
            {
                'L' => light,
                'S' => stone,
                'D' or 'r' => dark,
                'M' => moss,
                _ => Color.clear,
            };
        });
    }

    private static Sprite RuneOf(string[] rows) => MakeColored(rows[0].Length + 2, rows.Length + 2, 14f, (x, y) =>
        x >= 1 && y >= 1 && x <= rows[0].Length && y <= rows.Length && Px(rows, x - 1, y - 1) == 'r' ? Color.white : Color.clear);

    private static readonly Sprite[] boonIcons = new Sprite[4];

    private static readonly string[][] BoonRows =
    {
        new[] { ".......##", "......###", ".....###.", "....###..", "#..###...", ".####....", "..##.....", ".#.##....", "#...#...." },
        new[] { "....####.", "...####..", "..####...", ".#######.", "...####..", "..####...", ".###.....", ".##......", "#........" },
        new[] { ".###.###.", ".sss.sss.", ".###.###.", ".###.###.", ".###.###.", ".###.###.", ".#######.", "..#####..", "...###..." },
        new[] { ".##...##.", "####.####", "#########", "#########", ".#######.", "..#####..", "...###...", "....#....", "........." },
    };

    public static Sprite BoonIcon(int boon) => boonIcons[boon] != null ? boonIcons[boon] : boonIcons[boon] = MakeOutlined(9, 9, 12f, (x, y) =>
        BoonRows[boon][8 - y][x] switch { '#' => Color.white, 's' => new Color(0.7f, 0.7f, 0.75f), _ => Color.clear });

    private static Sprite starburst, sparkle, pixelPuff, furTuft;
    private static readonly string[] StarRows =
    {
        ".....#.....", ".#...#...#.", "..#..#..#..", "...#.#.#...", "....###....", "###########",
        "....###....", "...#.#.#...", "..#..#..#..", ".#...#...#.", ".....#.....",
    };
    private static readonly string[] SparkleRows = { "...#...", "...#...", "..###..", "#######", "..###..", "...#...", "...#..." };
    private static readonly string[] PuffRows = { "..##..", ".####.", "######", "######", ".####." };
    private static readonly string[] FurRows = { "#...", ".#..", ".##.", "..##" };
    public static Sprite Starburst => starburst != null ? starburst : starburst = Rows(StarRows, 14f);
    public static Sprite Sparkle => sparkle != null ? sparkle : sparkle = Rows(SparkleRows, 20f);
    public static Sprite PixelPuff => pixelPuff != null ? pixelPuff : pixelPuff = Rows(PuffRows, 16f);
    public static Sprite FurTuft => furTuft != null ? furTuft : furTuft = Rows(FurRows, 20f);
    private static Sprite Rows(string[] rows, float ppu) => MakeColored(rows[0].Length, rows.Length, ppu, (x, y) =>
        Px(rows, x, y) == '#' ? Color.white : Color.clear);

    private static readonly string[] PawRows = { ".#.#.", "#...#", ".###.", ".###." };
    public static Sprite PawPrint => pawPrint != null ? pawPrint : pawPrint = MakeOutlined(5, 4, 14f, (x, y) =>
        Px(PawRows, x, y) == '#' ? Color.white : Color.clear);

    public static Sprite Sack => sack != null ? sack : sack = MakeOutlined(10, 10, 16f, (x, y) =>
    {
        if (y == 9 && x == 6) return new Color(1f, 0.85f, 0.3f);
        if (y == 8 && x >= 3 && x <= 6) return x == 6 ? new Color(1f, 0.85f, 0.3f) : new Color(0.66f, 0.47f, 0.28f);
        if (y == 7 && x >= 4 && x <= 5) return new Color(0.35f, 0.22f, 0.12f);
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

    private static Sprite heart;
    private static readonly string[] HeartRows = { ".##.##.", "#w#####", "#######", ".#####.", "..###..", "...#..." };

    public static Sprite Heart => heart != null ? heart : heart = MakeOutlined(7, 6, 26f, (x, y) =>
    {
        char c = HeartRows[5 - y][x];
        return c == 'w' ? new Color(1f, 0.85f, 0.92f) : c == '#' ? new Color(1f, 0.36f, 0.55f) : Color.clear;
    });

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

    public static Sprite Projectile(string path, int doublings = 0)
    {
        string key = path + "#shot" + doublings;
        if (art.TryGetValue(key, out Sprite s) && s != null) return s;
        var src = Resources.Load<Texture2D>(path);
        if (src == null || !src.isReadable) return Art(path);

        int w = src.width, h = src.height;
        Color32[] p = src.GetPixels32();
        bool Pink(Color32 c) => c.a > 128 && c.r > 216 && c.g < 166 && c.b > 140;
        bool Dark(Color32 c) => c.a > 128 && c.r + c.g + c.b < 153;
        for (int i = 0; i < p.Length; i++) if (Pink(p[i])) p[i] = default;
        var keep = (Color32[])p.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!Dark(p[y * w + x])) continue;
                bool touchesArt = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        Color32 n = p[ny * w + nx];
                        if (n.a > 128 && !Dark(n)) touchesArt = true;
                    }
                if (!touchesArt) keep[y * w + x] = default;
            }
        return art[key] = Upscaled(keep, w, h, doublings);
    }

    public static Sprite Cloud
    {
        get
        {
            if (art.TryGetValue("cloud#2x", out Sprite s) && s != null) return s;
            var src = Resources.Load<Texture2D>("cloud");
            if (src == null || !src.isReadable) return Art("cloud");
            return art["cloud#2x"] = Upscaled(src.GetPixels32(), src.width, src.height, 1);
        }
    }

    private static Sprite Upscaled(Color32[] px, int w, int h, int doublings)
    {
        for (int n = 0; n < doublings; n++)
        {
            px = Scale2x(px, w, h);
            w *= 2;
            h *= 2;
        }
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        RectInt r = OpaqueBounds(tex);
        return Sprite.Create(tex, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f), Mathf.Max(r.width, r.height));
    }

    private static Color32[] Scale2x(Color32[] src, int w, int h)
    {
        var dst = new Color32[w * h * 4];
        int W = w * 2;
        Color32 At(int x, int y) => src[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)];
        bool Eq(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = At(x, y), up = At(x, y + 1), down = At(x, y - 1), left = At(x - 1, y), right = At(x + 1, y);
                bool corner = !Eq(up, down) && !Eq(left, right);
                dst[(2 * y + 1) * W + 2 * x] = corner && Eq(left, up) ? left : c;
                dst[(2 * y + 1) * W + 2 * x + 1] = corner && Eq(up, right) ? right : c;
                dst[2 * y * W + 2 * x] = corner && Eq(left, down) ? left : c;
                dst[2 * y * W + 2 * x + 1] = corner && Eq(down, right) ? right : c;
            }
        return dst;
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
