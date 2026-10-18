using System.Collections.Generic;
using UnityEngine;

// Shockwave Stomp's ground slam, drawn in code like the lava (LavaArt), pixel art:
//   a cracked crater left on the ground (fades out), a dusty shockwave ring rolling out to the hit radius,
//   and chunks of earth thrown out in little arcs.
// Frost Nova uses the same art in ice colors (ice: true): frost veins spread over a frosted patch, an icy ring, no chunks.
public class StompFx : MonoBehaviour
{
    private const int CrackVariants = 3, CrackSize = 128, RingSize = 96;
    private const float CrackLife = 1.1f, RingTime = 0.28f, ChunkLife = 0.7f;
    private static Sprite[] cracks, iceCracks;
    private static Sprite ring, iceRing;
    private static Sprite[] chunks;

    // earthy palette
    private static readonly Color Gap = new Color(0.14f, 0.09f, 0.06f);   // crack depth
    private static readonly Color Lip = new Color(0.62f, 0.5f, 0.34f);    // lit crack edge
    private static readonly Color Pit = new Color(0.3f, 0.21f, 0.13f);    // crater floor
    private static readonly Color Rim = new Color(0.72f, 0.6f, 0.42f);    // crater rim
    private static readonly Color Dust = new Color(0.86f, 0.77f, 0.58f);

    // { crack, crack lip, crater floor, crater rim, ring bright, ring inner, frost fill (alpha 0 = none) }
    private static readonly Color[] Earth = { Gap, Lip, Pit, Rim, Dust, new Color(0.72f, 0.6f, 0.43f), Color.clear };
    private static readonly Color[] Ice =
    {
        new Color(0.62f, 0.86f, 1f), Color.white, new Color(0.5f, 0.78f, 1f), new Color(0.88f, 0.97f, 1f),
        new Color(0.8f, 0.95f, 1f), new Color(0.5f, 0.78f, 1f), new Color(0.75f, 0.92f, 1f, 0.3f),
    };

    private SpriteRenderer crack, wave;
    private readonly List<Chunk> flying = new List<Chunk>();
    private float age, radius;

    private class Chunk { public SpriteRenderer sr; public Vector2 pos, vel; public float z, vz; }

    public static void Spawn(Vector2 pos, float radius, bool ice = false)
    {
        Prewarm();
        var fx = new GameObject("StompFx").AddComponent<StompFx>();
        fx.transform.position = pos;
        fx.radius = radius;

        // cracks: on the ground with the lava (over grass, under rocks / trees / characters), quarter turns keep pixels crisp
        fx.crack = Layer(fx.transform, (ice ? iceCracks : cracks)[Random.Range(0, CrackVariants)], -84, radius * 2f * 0.85f);
        fx.crack.transform.rotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
        fx.wave = Layer(fx.transform, ice ? iceRing : ring, 9, 0.1f);
        if (ice) Lighting.Glow(fx.wave); // ice ring shines a little

        int n = ice ? 0 : Mathf.RoundToInt(8 + radius * 2f); // the nova throws its own ice shards
        if (Settings.LowParticles) n /= 2;
        for (int i = 0; i < n; i++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, i * 360f / n + Random.Range(-15f, 15f)) * Vector2.right;
            var c = new Chunk
            {
                sr = Layer(fx.transform, chunks[Random.Range(0, chunks.Length)], 9, Random.Range(0.18f, 0.3f)),
                pos = pos + dir * Random.Range(0.2f, 0.6f),
                vel = dir * Random.Range(radius * 0.9f, radius * 1.6f),
                vz = Random.Range(2.5f, 4.5f),
            };
            c.sr.flipX = Random.value < 0.5f;
            fx.flying.Add(c);
        }
    }

    private static SpriteRenderer Layer(Transform parent, Sprite sprite, int order, float size)
    {
        var sr = new GameObject("Layer").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.transform.localScale = Vector3.one * size;
        return sr;
    }

    private void Update()
    {
        age += Time.deltaTime;

        // the crack bites in fast, sits, then fades
        float crackFade = 1f - Mathf.Clamp01((age - CrackLife * 0.45f) / (CrackLife * 0.55f));
        crack.color = new Color(1f, 1f, 1f, crackFade);
        crack.transform.localScale = Vector3.one * radius * 2f * 0.85f * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(age / 0.06f));

        // dust ring rolls out to the hit radius, easing off and thinning
        float t = Mathf.Clamp01(age / RingTime);
        float eased = 1f - (1f - t) * (1f - t);
        wave.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2.15f, eased);
        wave.color = new Color(1f, 1f, 1f, 1f - t * t);

        // earth chunks: fly out on an arc, one small bounce, then settle and vanish
        foreach (Chunk c in flying)
        {
            if (!c.sr.enabled) continue;
            c.vz -= 16f * Time.deltaTime;
            c.z += c.vz * Time.deltaTime;
            if (c.z < 0f) { c.z = 0f; c.vz = c.vz < -1.5f ? -c.vz * 0.35f : 0f; c.vel *= 0.5f; }
            c.vel *= 1f - 2.5f * Time.deltaTime;
            c.pos += c.vel * Time.deltaTime;
            c.sr.transform.position = c.pos + Vector2.up * c.z;
            c.sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((age - ChunkLife * 0.6f) / (ChunkLife * 0.4f)));
        }

        if (age >= CrackLife) Destroy(gameObject);
    }

    // ---------- art ----------

    // Drawn once while the level loads (EnemySpawner prewarm), like the lava.
    public static void Prewarm()
    {
        if (cracks != null) return;
        cracks = new Sprite[CrackVariants];
        iceCracks = new Sprite[CrackVariants];
        for (int v = 0; v < CrackVariants; v++) { cracks[v] = DrawCrack(v, Earth); iceCracks[v] = DrawCrack(v, Ice); }
        ring = DrawRing(Earth);
        iceRing = DrawRing(Ice);
        chunks = new[] { DrawChunk(0), DrawChunk(1), DrawChunk(2) };
    }

    private static Sprite Make(Color[] px, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); // 1 unit wide
    }

    // Radial cracks: jagged random walks out from a small crater, tapering, with branches;
    // each crack pixel gets a lit lip on one side so it reads as a groove.
    private static Sprite DrawCrack(int v, Color[] pal)
    {
        Color Gap = pal[0], Lip = pal[1], Pit = pal[2], Rim = pal[3], Frost = pal[6];
        int s = CrackSize;
        var px = new Color[s * s];
        var rng = new System.Random(500 + v * 13);
        float R() => (float)rng.NextDouble();
        float c = (s - 1) / 2f, maxR = s * 0.48f, pit = s * 0.09f;

        void Plot(int x, int y, Color col) { if (x >= 0 && y >= 0 && x < s && y < s) px[y * s + x] = col; }
        void Groove(float x, float y, int width)
        {
            int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
            for (int w = 0; w < width; w++) { Plot(ix + w, iy, Gap); Plot(ix, iy + w, Gap); }
            if (iy + width < s && px[(iy + width) * s + ix].a == 0f) Plot(ix, iy + width, Lip); // light from above
        }
        void Walk(float x, float y, float ang, float length, float width, int depth)
        {
            for (float i = 0; i < length; i++)
            {
                ang += (R() - 0.5f) * 0.55f; // jag
                x += Mathf.Cos(ang);
                y += Mathf.Sin(ang);
                float k = 1f - i / length;
                Groove(x, y, Mathf.Max(1, Mathf.RoundToInt(width * k)));
                if (depth < 2 && R() < 0.035f) // branch off
                    Walk(x, y, ang + (R() < 0.5f ? -1f : 1f) * (0.4f + R() * 0.5f), (length - i) * (0.35f + R() * 0.25f), width * k * 0.7f, depth + 1);
            }
        }

        int count = 7 + v;
        float turn = R() * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = turn + i * Mathf.PI * 2f / count + (R() - 0.5f) * 0.5f;
            Walk(c + Mathf.Cos(a) * pit, c + Mathf.Sin(a) * pit, a, (maxR - pit) * (0.55f + R() * 0.45f), 2.6f, 0);
        }

        // crater: dark floor, light rim, a few loose pebbles
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                if (d < pit - 1.5f) px[y * s + x] = Pit;
                else if (d < pit + 0.8f) px[y * s + x] = Rim;
            }
        for (int i = 0; i < 26; i++)
        {
            float a = R() * Mathf.PI * 2f, d = pit + 2f + R() * maxR * 0.6f;
            int x = Mathf.RoundToInt(c + Mathf.Cos(a) * d), y = Mathf.RoundToInt(c + Mathf.Sin(a) * d);
            Plot(x, y, R() < 0.5f ? Rim : Lip);
        }

        // ice: a frosted patch under the veins, ragged at the edge, with glints
        if (Frost.a > 0f)
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    if (px[y * s + x].a > 0f) continue;
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / maxR;
                    if (d > 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.12f + v * 9f, y * 0.12f)) continue;
                    px[y * s + x] = R() < 0.02f ? Color.white : new Color(Frost.r, Frost.g, Frost.b, Frost.a * (1f - 0.5f * d));
                }
        return Make(px, s);
    }

    // Shockwave: a thick, dusty, slightly broken ring, lighter on its outer edge.
    private static Sprite DrawRing(Color[] pal)
    {
        int s = RingSize;
        var px = new Color[s * s];
        float c = (s - 1) / 2f, outer = s * 0.49f, inner = s * 0.36f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                if (d > outer || d < inner) continue;
                float k = (d - inner) / (outer - inner); // 0 inner .. 1 outer
                float n = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                if (n < 0.28f && k < 0.6f) continue; // gaps in the dust toward the inside
                Color col = k > 0.82f ? Color.white : k > 0.55f ? pal[4] : pal[5];
                col.a = k > 0.55f ? 1f : 0.75f;
                px[y * s + x] = col;
            }
        return Make(px, s);
    }

    // Tiny rock chunks: an 8px blob, shaded light top-left, dark bottom-right.
    private static Sprite DrawChunk(int v)
    {
        const int s = 8;
        var px = new Color[s * s];
        var rng = new System.Random(7 + v);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f)) + (float)rng.NextDouble() * 1.2f;
                if (d > 3.9f) continue;
                px[y * s + x] = x + (s - y) < 7 ? Rim : x - y > 2 ? Pit : new Color(0.5f, 0.38f, 0.25f);
            }
        return Make(px, s);
    }
}
