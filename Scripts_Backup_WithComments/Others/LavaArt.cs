using UnityEngine;

// HoloCure-style lava pool, drawn in code (no art files): a lumpy blob with a dark crust rim, flowing
// orange-to-yellow magma, cooled dark crust bits and bubbles that swell and pop. A few shape variants,
// each a looping animation. Point-filtered pixel art.
public static class LavaArt
{
    public const int Variants = 3, Frames = 8;
    // two resolutions so pools of any size have pixels close to the map tiles': small pools and big (max Molotov) ones
    private const int SmallSize = 64, BigSize = 128;
    public const float BigFrom = 1.6f; // pool radius (units) from which the big art is used
    public const int LavaPal = 0, GluePal = 1, TarPal = 2;
    private static Sprite[][] small, big; // [palette]

    // Palettes, dark to bright: edge, crust, deep, mid, bright, light, highlight
    private static readonly Color[] Lava =
    {
        new Color(0.22f, 0.04f, 0.04f), new Color(0.45f, 0.08f, 0.05f), new Color(0.78f, 0.17f, 0.05f), new Color(0.98f, 0.4f, 0.07f),
        new Color(1f, 0.6f, 0.12f), new Color(1f, 0.83f, 0.27f), new Color(1f, 0.97f, 0.65f),
    };
    // Glue Bomb goo: the same bubbling pool in sticky slime green, with glossy near-white highlights
    private static readonly Color[] Glue =
    {
        new Color(0.13f, 0.3f, 0.1f), new Color(0.24f, 0.5f, 0.15f), new Color(0.36f, 0.7f, 0.2f), new Color(0.48f, 0.86f, 0.28f),
        new Color(0.6f, 0.95f, 0.38f), new Color(0.76f, 1f, 0.56f), new Color(0.95f, 1f, 0.88f),
    };
    // Tar Pit: black bubbling tar with veins of fire showing through
    private static readonly Color[] Tar =
    {
        new Color(0.04f, 0.02f, 0.02f), new Color(0.1f, 0.06f, 0.04f), new Color(0.17f, 0.1f, 0.06f), new Color(0.26f, 0.14f, 0.07f),
        new Color(0.36f, 0.19f, 0.08f), new Color(0.95f, 0.4f, 0.07f), new Color(1f, 0.72f, 0.25f),
    };
    private static readonly Color[][] Palettes = { Lava, Glue, Tar };

    public static Sprite Get(int variant, int frame, float radius, int palette = LavaPal)
    {
        Prewarm();
        Sprite[] set = (radius >= BigFrom ? big : small)[palette];
        return set[(variant % Variants) * Frames + (frame % Frames)];
    }

    // Drawing every frame takes a moment: done once, while the level loads (EnemySpawner), not mid-fight.
    public static void Prewarm()
    {
        if (small != null) return;
        small = new Sprite[Palettes.Length][];
        big = new Sprite[Palettes.Length][];
        for (int p = 0; p < Palettes.Length; p++)
        {
            small[p] = Build(SmallSize, Palettes[p]);
            big[p] = Build(BigSize, Palettes[p]);
        }
    }

    private static Sprite[] Build(int size, Color[] pal)
    {
        var set = new Sprite[Variants * Frames];
        for (int v = 0; v < Variants; v++)
            for (int f = 0; f < Frames; f++)
                set[v * Frames + f] = Draw(v, f, size, pal);
        return set;
    }

    private static Sprite Draw(int v, int f, int Size, Color[] pal)
    {
        Color Edge = pal[0], Crust = pal[1], Deep = pal[2], Orange = pal[3], Bright = pal[4], Yellow = pal[5], Hot = pal[6];
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[Size * Size];
        float c = (Size - 1) / 2f, maxR = Size * 0.47f;
        float loop = f / (float)Frames * Mathf.PI * 2f;
        Vector2 flow = new Vector2(Mathf.Cos(loop), Mathf.Sin(loop)) * 2.2f; // circular drift: the animation loops seamlessly
        float seed = v * 37.1f;
        float k = Size / 48f; // pattern scale: tuned at 48px

        // bubbles: fixed spots per variant, each swelling and popping once per loop at its own phase
        var bubbles = new Vector3[4];
        var rng = new System.Random(91 + v);
        for (int i = 0; i < bubbles.Length; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = (float)rng.NextDouble() * 0.55f;
            bubbles[i] = new Vector3(c + Mathf.Cos(a) * d * maxR, c + Mathf.Sin(a) * d * maxR, (float)rng.NextDouble());
        }

        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x - c, dy = y - c;
                float ang = Mathf.Atan2(dy, dx);
                // lumpy outline, wobbling slightly as it bubbles
                float r = maxR * (1f + 0.09f * Mathf.Sin(3f * ang + seed) + 0.06f * Mathf.Sin(5f * ang + seed * 1.7f)
                                     + 0.035f * Mathf.Sin(8f * ang + seed * 0.3f) + 0.015f * Mathf.Sin(4f * ang + loop)) * 0.86f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (d > 1f) { px[y * Size + x] = Color.clear; continue; }

                Color col;
                if (d > 0.93f) col = Edge;
                else if (d > 0.82f) col = Crust;
                else if (d > 0.72f) col = Deep;
                else
                {
                    // flowing magma: two noise layers drifting in a loop, brighter toward the middle
                    float n = Mathf.PerlinNoise(x / k * 0.13f + flow.x + seed, y / k * 0.13f + flow.y + seed);
                    float fine = Mathf.PerlinNoise(x / k * 0.32f - flow.y * 1.5f + seed, y / k * 0.32f + flow.x * 1.5f);
                    float heat = n * 0.75f + fine * 0.25f + (1f - d) * 0.22f;
                    col = heat > 0.82f ? Hot : heat > 0.7f ? Yellow : heat > 0.56f ? Bright : heat > 0.4f ? Orange : Deep;
                    // cooled crust bits floating on top
                    float crust = Mathf.PerlinNoise(x / k * 0.2f + flow.x * 0.6f + 50f + seed, y / k * 0.2f - flow.y * 0.6f + 50f);
                    if (crust < 0.22f && d > 0.25f) col = crust < 0.15f ? Crust : Deep;
                }

                // bubbles: a dark ring with a bright cap, growing then popping
                foreach (Vector3 b in bubbles)
                {
                    float phase = Mathf.Repeat(f / (float)Frames + b.z, 1f);
                    if (phase > 0.75f) continue; // popped, waiting to come back
                    float br = (1f + phase * 3.4f) * k;
                    float bd = Vector2.Distance(new Vector2(x, y), new Vector2(b.x, b.y));
                    if (bd > br + 0.6f * k) continue;
                    if (bd > br - 0.6f * k) col = Deep;
                    else col = (x - b.x) < -br * 0.3f && (y - b.y) > br * 0.3f ? Hot : Yellow; // highlight top-left
                }
                px[y * Size + x] = col;
            }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size); // 1 unit wide, like the old circle
    }
}
