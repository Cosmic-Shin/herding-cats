using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    [Header("Size")]
    public int halfSize = 48;
    public bool menuMap;
    public int outerMargin = 3;
    public int seed = 1337;

    [Header("Ground tiles (Ground Tileset, cut 32x32)")]
    public Sprite grass;
    public Sprite[] clearing = new Sprite[4];
    public Sprite[] dirtSquare = new Sprite[4];
    public Sprite[] pebbles;
    public Sprite[] cobbles;

    [Header("Ponds (Water Tileset, cut 32x32)")]
    public Sprite water;
    public Sprite[] pondCorners = new Sprite[4];
    public Sprite pondTop, pondBottom, pondLeft, pondRight;
    public Sprite[] reeds, lilies, glints;
    public int ponds = 9;

    [Header("Props")]
    public Sprite[] decor;
    public Sprite[] rareDecor;
    [Range(0f, 1f)] public float rareDecorShare = 0.25f;
    public Sprite[] bushes;
    public Sprite[] rocks;
    public Sprite[] logs;
    public Sprite[] trees;

    [Header("Density (per 16x16 tiles)")]
    public float decorPerBlock = 34f;
    public float bushesPerBlock = 2.5f;
    public float rocksPerBlock = 1.5f;
    public float logsPerBlock = 1f;
    public float maxTreesPerBlock = 5f;
    public float clearingChance = 0.5f;
    public float pebbleChance = 0.02f;
    public float startAreaRadius = 5f;

    public static Rect Bounds { get; private set; }
    public static bool Active => instance != null;
    public static Vector2? MenuCenter { get; private set; }
    public static int ObstacleMask { get; private set; }
    public static int TallMask { get; private set; }
    public const float WallPad = 1.2f;
    public static readonly string[] Names = { "Meadow", "Winter", "Volcano" };
    public static readonly string[] Blurbs =
    {
        "<color=#7CFF9A>EASY</color>\nGreen fields, trees and ponds.\nNo hazards: the classic cat hunt.",
        "<color=#B3E6FF>TRICKY</color>\nSnow, frosted trees and frozen ponds.\nYou can cross the ice, but everything on it slides, you included.",
        "<color=#FF7326>HARD</color>\nAsh fields and bubbling lava.\nLava burns you and cats alike, and eruptions rain down near you more and more often.",
    };
    public static int Choice => Mathf.Clamp(PlayerPrefs.GetInt("map", 0), 0, Names.Length - 1);
    public static int Biome { get; private set; }
    public static bool Winter => Biome == 1;
    public static bool Volcano => Biome == 2;

    public const float IceGrip = 9f;
    private static bool[] ice;
    private static int iceMin, iceN;
    public static bool OnIce(Vector2 p) => Winter && OnPond(p);
    public static bool OnLava(Vector2 p) => Volcano && OnPond(p);

    private static bool OnPond(Vector2 p)
    {
        if (ice == null) return false;
        int x = Mathf.FloorToInt(p.x) - iceMin, y = Mathf.FloorToInt(p.y) - iceMin;
        return x >= 0 && y >= 0 && x < iceN && y < iceN && ice[x + y * iceN];
    }

    private static MapGenerator instance;

    private const int GroundOrder = -100, OverlayOrder = -99, DecorOrder = -90, MediumOrder = -80, TreeOrder = -70, GlowOrder = 9;

    private Tilemap ground, overlay;
    private readonly Dictionary<Sprite, Tile> tiles = new Dictionary<Sprite, Tile>();
    private int obstacleLayer, tallLayer;
    private System.Random rng;
    private float R() => (float)rng.NextDouble();
    private T Pick<T>(T[] a) => a[rng.Next(a.Length)];

    public static Vector2 ClampInside(Vector2 p, float pad = 0.5f)
    {
        if (instance == null) return p;
        Rect b = Bounds;
        return new Vector2(Mathf.Clamp(p.x, b.xMin + pad, b.xMax - pad), Mathf.Clamp(p.y, b.yMin + pad, b.yMax - pad));
    }

    public static bool Blocked(Vector2 p, float radius) =>
        instance != null && (Physics2D.OverlapCircle(p, radius, ObstacleMask) != null || ClampInside(p, Mathf.Max(radius, WallPad)) != p);

    public static Vector2 PlaceDrop(Vector2 p)
    {
        if (instance == null) return p;
        p = ClampInside(p, WallPad);
        if (Physics2D.OverlapCircle(p, 0.3f, ObstacleMask) == null) return p;
        for (float r = 0.75f; r <= 6f; r += 0.75f)
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector2 c = ClampInside(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, WallPad);
                if (Physics2D.OverlapCircle(c, 0.3f, ObstacleMask) == null) return c;
            }
        return p;
    }

    private void Awake()
    {
        Biome = Choice;
        if (Biome != 0) ponds = Mathf.RoundToInt(ponds * (Winter ? 2.2f : 1.6f));
        if (menuMap) MenuCenter = transform.position;
        else
        {
            instance = this;
            Bounds = new Rect(-halfSize, -halfSize, halfSize * 2f, halfSize * 2f);
        }
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0) obstacleLayer = 0;
        tallLayer = LayerMask.NameToLayer("TallObstacle");
        if (tallLayer < 0) tallLayer = obstacleLayer;
        TallMask = 1 << tallLayer;
        ObstacleMask = (1 << obstacleLayer) | TallMask;
        for (int i = 0; i < 32; i++)
        {
            Physics2D.IgnoreLayerCollision(obstacleLayer, i, true);
            Physics2D.IgnoreLayerCollision(tallLayer, i, true);
        }

        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        ground = Layer(grid.transform, "Ground", GroundOrder);
        overlay = Layer(grid.transform, "Overlay", OverlayOrder);
        rng = new System.Random(seed);
        Build();
        if (!menuMap) BuildGlow();
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; ice = null; }
        if (menuMap) MenuCenter = null;
    }

    private static Tilemap Layer(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        return map;
    }

    private void Build()
    {
        int min = -halfSize - outerMargin, n = 2 * (halfSize + outerMargin);
        var groundTiles = new TileBase[n * n];
        var overlayTiles = new TileBase[n * n];
        var isWater = new bool[n * n];
        int Index(int x, int y) => (x - min) + (y - min) * n;
        bool InGrid(int x, int y) => x >= min && y >= min && x < min + n && y < min + n;
        bool WaterAt(Vector2 p, int pad)
        {
            int tx = Mathf.FloorToInt(p.x), ty = Mathf.FloorToInt(p.y);
            for (int x = tx - pad; x <= tx + pad; x++)
                for (int y = ty - pad; y <= ty + pad; y++)
                    if (InGrid(x, y) && isWater[Index(x, y)]) return true;
            return false;
        }

        Tile grassTile = TileFor(grass);
        for (int i = 0; i < groundTiles.Length; i++) groundTiles[i] = grassTile;

        var pondRects = new List<RectInt>();
        for (int attempt = 0; attempt < ponds * 30 && pondRects.Count < ponds; attempt++)
        {
            int w = Biome != 0 ? rng.Next(4, 12) : rng.Next(3, 9), h = Biome != 0 ? rng.Next(4, 9) : rng.Next(3, 7);
            var r = new RectInt(rng.Next(-halfSize + 3, halfSize - 3 - w), rng.Next(-halfSize + 3, halfSize - 3 - h), w, h);
            var padded = new RectInt(r.x - 3, r.y - 3, r.width + 6, r.height + 6);
            if (pondRects.Exists(o => o.Overlaps(padded))) continue;
            Vector2 nearest = new Vector2(Mathf.Clamp(0f, r.xMin, r.xMax), Mathf.Clamp(0f, r.yMin, r.yMax));
            if (nearest.magnitude < startAreaRadius + 3f) continue;
            pondRects.Add(r);
        }
        foreach (RectInt r in pondRects)
        {
            for (int x = r.xMin; x < r.xMax; x++)
                for (int y = r.yMin; y < r.yMax; y++)
                {
                    bool l = x == r.xMin, rt = x == r.xMax - 1, b = y == r.yMin, t = y == r.yMax - 1;
                    Sprite s = t && l ? pondCorners[0] : t && rt ? pondCorners[1] : b && l ? pondCorners[2] : b && rt ? pondCorners[3]
                             : t ? pondTop : b ? pondBottom : l ? pondLeft : rt ? pondRight : water;
                    int i = Index(x, y);
                    groundTiles[i] = TileFor(s);
                    isWater[i] = true;
                    bool edge = l || rt || b || t;
                    if (edge && reeds.Length > 0 && R() < 0.15f) overlayTiles[i] = TileFor(Pick(reeds));
                    else if (!edge && lilies.Length > 0 && R() < 0.14f && Biome == 0) overlayTiles[i] = TileFor(Pick(lilies));
                    else if (!edge && glints.Length > 0 && R() < (Winter ? 0.1f : 0.06f)) overlayTiles[i] = TileFor(Pick(glints));
                }
            if (Biome != 0) { if (Volcano) LavaGlow(r); continue; }
            WaterShimmer(r);
            var go = new GameObject("Pond");
            go.layer = tallLayer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(r.width - 0.5f, r.height - 0.5f);
        }

        if (Biome != 0 && !menuMap) { ice = isWater; iceMin = min; iceN = n; }

        const int Block = 16;
        bool Dry(int x, int y, int w, int h)
        {
            for (int i = x - 1; i <= x + w; i++)
                for (int j = y - 1; j <= y + h; j++)
                    if (!InGrid(i, j) || isWater[Index(i, j)]) return false;
            return true;
        }
        void Patch2x2(Sprite[] parts, int x, int y)
        {
            if (!Dry(x, y, 2, 2)) return;
            groundTiles[Index(x, y + 1)] = TileFor(parts[0]);
            groundTiles[Index(x + 1, y + 1)] = TileFor(parts[1]);
            groundTiles[Index(x, y)] = TileFor(parts[2]);
            groundTiles[Index(x + 1, y)] = TileFor(parts[3]);
        }
        for (int bx = -halfSize; bx < halfSize; bx += Block)
            for (int by = -halfSize; by < halfSize; by += Block)
            {
                if (R() < clearingChance) Patch2x2(clearing, bx + rng.Next(1, Block - 2), by + rng.Next(1, Block - 2));
                if (R() < clearingChance * 0.6f) Patch2x2(dirtSquare, bx + rng.Next(1, Block - 2), by + rng.Next(1, Block - 2));
                if (cobbles.Length > 0 && R() < 0.15f)
                {
                    int cx = bx + rng.Next(0, Block - 3), cy = by + rng.Next(0, Block - 3), cw = rng.Next(2, 4), ch = rng.Next(2, 4);
                    if (Dry(cx, cy, cw, ch))
                        for (int x = cx; x < cx + cw; x++)
                            for (int y = cy; y < cy + ch; y++)
                                if (R() < 0.85f) overlayTiles[Index(x, y)] = TileFor(Pick(cobbles));
                }
            }
        for (int i = 0; i < overlayTiles.Length; i++)
            if (groundTiles[i] == grassTile && overlayTiles[i] == null && R() < pebbleChance) overlayTiles[i] = TileFor(Pick(pebbles));

        var changes = new TileChangeData[n * n];
        for (int i = 0; i < changes.Length; i++)
        {
            int x = min + i % n, y = min + i / n;
            changes[i] = new TileChangeData(new Vector3Int(x, y, 0), groundTiles[i], GroundTint(x, y), Matrix4x4.identity);
        }
        ground.SetTiles(changes, true);
        var all = new BoundsInt(new Vector3Int(min, min, 0), new Vector3Int(n, n, 1));
        overlay.SetTilesBlock(all, overlayTiles);

        Transform props = new GameObject("Props").transform;
        props.SetParent(transform, false);
        float blocks = (halfSize * 2f / Block) * (halfSize * 2f / Block);
        Vector2 Spot(float pad) => new Vector2(-halfSize + pad + R() * (halfSize * 2f - pad * 2f), -halfSize + pad + R() * (halfSize * 2f - pad * 2f));
        var solids = new List<Vector2>();
        bool Clear(Vector2 p, float gap) => p.magnitude > startAreaRadius && !WaterAt(p, 1) && !solids.Exists(s => (s - p).sqrMagnitude < gap * gap);

        for (int i = 0, count = Mathf.RoundToInt(decorPerBlock * blocks); i < count; i++)
        {
            Vector2 p = Spot(0.3f);
            if (!WaterAt(p, 0)) Prop(props, R() < rareDecorShare ? Pick(rareDecor) : Pick(decor), p, DecorOrder);
        }
        for (int i = 0, count = Mathf.RoundToInt(bushesPerBlock * blocks); i < count; i++)
        {
            Vector2 p = Spot(2f);
            if (!WaterAt(p, 1)) Sway(Prop(props, Pick(bushes), p, MediumOrder), false);
        }
        void Solids(Sprite[] set, float perBlock, bool tree)
        {
            for (int i = 0, count = Mathf.RoundToInt(perBlock * blocks); i < count; i++)
            {
                Vector2 p = Spot(2.5f);
                if (tree && R() > Grove(p)) continue;
                if (!Clear(p, tree ? 2.2f : 1.6f)) continue;
                solids.Add(p);
                SpriteRenderer sr = Prop(props, Pick(set), p, tree ? TreeOrder + Mathf.RoundToInt(Mathf.Repeat(-p.y, 16f)) : MediumOrder);
                Solid(sr, tree);
                if (tree) Sway(sr, true);
            }
        }
        Solids(rocks, rocksPerBlock, false);
        Solids(logs, logsPerBlock, false);
        Solids(trees, maxTreesPerBlock, true);

        var smallTrees = trees.Length > 3 ? trees[3..] : trees;
        for (float s = -halfSize; s < halfSize; s += 1.4f + R() * 0.8f)
            foreach (Vector2 side in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                float inset = 0.4f + R() * 0.6f;
                Vector2 along = new Vector2(side.y, side.x);
                Vector2 p = side * (halfSize - inset) + along * (s + R() * 0.5f);
                bool tree = R() < 0.4f;
                Sway(Prop(props, tree ? Pick(smallTrees) : Pick(bushes), p, tree ? TreeOrder + Mathf.RoundToInt(Mathf.Repeat(-p.y, 16f)) : MediumOrder), tree);
            }
    }

    private float Grove(Vector2 p) => Mathf.Clamp01((Mathf.PerlinNoise(p.x * 0.023f + seed % 1000, p.y * 0.023f + seed % 997) - 0.35f) / 0.5f);

    private SpriteRenderer Prop(Transform parent, Sprite sprite, Vector2 at, int order)
    {
        if (sprite == null) return null;
        var go = new GameObject(sprite.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(Mathf.Round(at.x * 32f) / 32f, Mathf.Round(at.y * 32f) / 32f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Snowy(sprite);
        sr.sortingOrder = order;
        if (R() < 0.5f) go.transform.localScale = new Vector3(-1f, 1f, 1f);
        return sr;
    }

    private void Solid(SpriteRenderer sr, bool tree)
    {
        if (sr == null) return;
        if (!tree)
        {
            sr.gameObject.layer = obstacleLayer;
            sr.gameObject.AddComponent<PolygonCollider2D>();
            return;
        }
        sr.gameObject.layer = tallLayer;
        Rect trunk = TrunkBase(sr.sprite);
        var b = sr.gameObject.AddComponent<BoxCollider2D>();
        b.size = trunk.size;
        b.offset = trunk.center;
    }

    private readonly Dictionary<Sprite, Rect> trunks = new Dictionary<Sprite, Rect>();

    private Rect TrunkBase(Sprite s)
    {
        if (trunks.TryGetValue(s, out Rect cached)) return cached;
        Vector2 size = s.bounds.size;
        Rect fallback = new Rect(-size.x * 0.12f, -size.y * 0.45f, size.x * 0.24f, size.y * 0.15f);
        if (!s.texture.isReadable) return trunks[s] = fallback;

        Rect r = s.rect;
        int w = (int)r.width, h = (int)r.height;
        bool Brown(int x, int y)
        {
            Color c = s.texture.GetPixel((int)r.x + x, (int)r.y + y);
            return c.a > 0.5f && c.r > c.g * 1.15f && c.r > c.b * 1.3f;
        }
        int bottom = -1, top = -1;
        for (int y = 0; y < h / 2 && top < 0; y++)
            for (int x = 0; x < w; x++)
                if (Brown(x, y)) { if (bottom < 0) bottom = y; break; }
        if (bottom < 0) return trunks[s] = fallback;
        for (int y = h / 2; y > bottom; y--)
        {
            for (int x = 0; x < w && top < 0; x++) if (Brown(x, y)) top = y;
            if (top >= 0) break;
        }
        int bandTop = bottom + Mathf.Max(6, (top - bottom) / 3);
        int minX = w, maxX = -1;
        for (int y = bottom; y <= bandTop; y++)
            for (int x = 0; x < w; x++)
                if (Brown(x, y)) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }

        float ppu = s.pixelsPerUnit;
        Vector2 pivot = s.pivot;
        return trunks[s] = new Rect((minX - pivot.x) / ppu, (bottom - pivot.y) / ppu, (maxX - minX + 1) / ppu, (bandTop - bottom + 1) / ppu);
    }

    private Tile TileFor(Sprite s)
    {
        if (s == null) return null;
        if (!tiles.TryGetValue(s, out Tile t))
        {
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Padded(Snowy(s));
            t.flags = TileFlags.None;
            tiles[s] = t;
        }
        return t;
    }

    private static readonly Dictionary<(Texture2D, int), Texture2D> snowTextures = new Dictionary<(Texture2D, int), Texture2D>();
    private readonly Dictionary<Sprite, Sprite> snowSprites = new Dictionary<Sprite, Sprite>();

    private Sprite Snowy(Sprite s)
    {
        if (Biome == 0 || s == null) return s;
        if (snowSprites.TryGetValue(s, out Sprite cached)) return cached;
        if (!snowTextures.TryGetValue((s.texture, Biome), out Texture2D tex) || tex == null) snowTextures[(s.texture, Biome)] = tex = Recolor(s.texture);
        Sprite w = Sprite.Create(tex, s.rect, new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        w.name = s.name;
        return snowSprites[s] = w;
    }

    private static Texture2D Recolor(Texture2D src)
    {
        Texture2D copy = ReadableCopy(src);
        var tex = copy == src ? Object.Instantiate(src) : copy;
        Color32[] px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++) if (px[i].a > 0) px[i] = Volcano ? Ash(px[i]) : Snow(px[i]);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    public static Color32 Snow(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f, lum = 0.3f * r + 0.59f * g + 0.11f * b;
        Color o;
        if (b > r * 1.25f && b > g * 0.85f)
            o = Color.Lerp(new Color(0.5f, 0.74f, 0.95f), new Color(0.78f, 0.93f, 1f), Mathf.Clamp01((lum - 0.2f) / 0.5f));
        else if (g > r * 1.05f && g > b * 1.05f)
            o = Color.Lerp(new Color(0.52f, 0.6f, 0.76f), new Color(0.86f, 0.9f, 0.96f), Mathf.Clamp01((lum - 0.18f) / 0.42f));
        else
            o = Color.Lerp(new Color(r, g, b), new Color(lum * 0.95f, lum, lum * 1.1f), 0.25f);
        Color32 result = o;
        result.a = c.a;
        return result;
    }

    public static Color32 Ash(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f, lum = 0.3f * r + 0.59f * g + 0.11f * b;
        Color o;
        if (b > r * 1.25f && b > g * 0.85f)
            o = Color.Lerp(new Color(0.75f, 0.16f, 0.05f), new Color(1f, 0.78f, 0.25f), Mathf.Clamp01((lum - 0.25f) / 0.45f));
        else if (g > r * 1.05f && g > b * 1.05f)
            o = Color.Lerp(new Color(0.13f, 0.11f, 0.11f), new Color(0.4f, 0.35f, 0.33f), Mathf.Clamp01((lum - 0.15f) / 0.45f));
        else
            o = new Color(r * 0.6f + 0.05f, g * 0.48f + 0.02f, b * 0.42f + 0.02f);
        Color32 result = o;
        result.a = c.a;
        return result;
    }

    private readonly List<SpriteRenderer> lavaGlows = new List<SpriteRenderer>();
    private readonly List<(SpriteRenderer sr, int variant, float phase)> lavaBlobs = new List<(SpriteRenderer, int, float)>();

    private void LavaGlow(RectInt r)
    {
        for (float x = r.xMin + 1.5f; x <= r.xMax - 1.5f + 0.01f; x += 1.3f)
            for (float y = r.yMin + 1.5f; y <= r.yMax - 1.5f + 0.01f; y += 1.3f)
            {
                var blob = new GameObject("Lava").AddComponent<SpriteRenderer>();
                blob.transform.SetParent(transform, false);
                blob.transform.localPosition = new Vector2(x + R() * 0.4f - 0.2f, y + R() * 0.4f - 0.2f);
                blob.transform.localScale = Vector3.one * (2.6f + R() * 0.7f);
                blob.transform.rotation = Quaternion.Euler(0f, 0f, R() * 360f);
                blob.sortingOrder = OverlayOrder + 1;
                Lighting.Glow(blob);
                lavaBlobs.Add((blob, rng.Next(LavaArt.Variants), R() * 10f));
            }

        var sr = new GameObject("LavaGlow").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);
        sr.transform.localScale = new Vector3(r.width - 0.6f, r.height - 0.6f, 1f);
        sr.sprite = Shapes.Circle;
        sr.color = new Color(1f, 0.45f, 0.1f, 0.25f);
        sr.sortingOrder = OverlayOrder + 1;
        Lighting.Glow(sr);
        lavaGlows.Add(sr);

        lavaLights.Add(Lighting.MakeLight("LavaLight", sr.transform, 1f, 0.7f, new Color(1f, 0.45f, 0.15f)));
        lavaLights[lavaLights.Count - 1].pointLightOuterRadius = Mathf.Max(r.width, r.height) * 0.9f + 1.5f;
    }

    private readonly List<UnityEngine.Rendering.Universal.Light2D> lavaLights = new List<UnityEngine.Rendering.Universal.Light2D>();

    private class Swayer { public Transform t; public Vector2 anchor, top; public Vector3 scale; public bool tree; public float shake, amp, cooldown, phase; }
    private readonly List<Swayer> swayers = new List<Swayer>();
    private readonly Dictionary<Vector2Int, List<Swayer>> swayCells = new Dictionary<Vector2Int, List<Swayer>>();
    private const float SwayCell = 4f;

    private void Sway(SpriteRenderer sr, bool tree)
    {
        if (sr == null) return;
        Vector2 pos = sr.transform.position;
        float h = sr.sprite.bounds.extents.y;
        var s = new Swayer { t = sr.transform, tree = tree, scale = sr.transform.localScale, phase = R() * 6f,
                             anchor = tree ? pos + Vector2.down * h * 0.7f : pos, top = pos + Vector2.up * h * 0.5f };
        swayers.Add(s);
        var cell = new Vector2Int(Mathf.FloorToInt(s.anchor.x / SwayCell), Mathf.FloorToInt(s.anchor.y / SwayCell));
        if (!swayCells.TryGetValue(cell, out var list)) swayCells[cell] = list = new List<Swayer>();
        list.Add(s);
    }

    public static void Nudge(Vector2 pos, float radius, float strength)
    {
        if (instance == null) return;
        instance.NudgeAt(pos, radius, strength);
    }

    private void NudgeAt(Vector2 pos, float radius, float strength)
    {
        int x0 = Mathf.FloorToInt((pos.x - radius - 1f) / SwayCell), x1 = Mathf.FloorToInt((pos.x + radius + 1f) / SwayCell);
        int y0 = Mathf.FloorToInt((pos.y - radius - 1f) / SwayCell), y1 = Mathf.FloorToInt((pos.y + radius + 1f) / SwayCell);
        for (int cx = x0; cx <= x1; cx++)
            for (int cy = y0; cy <= y1; cy++)
            {
                if (!swayCells.TryGetValue(new Vector2Int(cx, cy), out var list)) continue;
                foreach (Swayer s in list)
                {
                    float reach = radius + (s.tree ? 0.7f : 0.6f);
                    if ((s.anchor - pos).sqrMagnitude > reach * reach) continue;
                    s.amp = Mathf.Max(s.amp * (s.shake > 0f ? 1f : 0f), strength);
                    s.shake = 0.5f;
                    if (s.cooldown > 0f) continue;
                    s.cooldown = 0.35f;
                    Color bit = Winter ? Color.white : Volcano ? new Color(0.4f, 0.37f, 0.35f) : new Color(0.4f, 0.7f, 0.28f);
                    Juice.Sparks(s.top + Random.insideUnitCircle * 0.3f, bit, Mathf.RoundToInt(2 + 4 * strength), 2.5f, 0.09f);
                }
            }
    }

    private void UpdateSway(float dt)
    {
        foreach (Swayer s in swayers)
        {
            if (s.cooldown > 0f) s.cooldown -= dt;
            if (s.shake <= 0f) continue;
            s.shake -= dt;
            float k = Mathf.Max(0f, s.shake / 0.5f) * s.amp;
            float wob = Mathf.Sin((0.5f - s.shake) * 38f + s.phase) * k;
            s.t.localRotation = Quaternion.Euler(0f, 0f, wob * (s.tree ? 3f : 7f));
            s.t.localScale = new Vector3(s.scale.x * (1f + 0.05f * wob), s.scale.y * (1f - 0.03f * Mathf.Abs(wob)), 1f);
            if (s.shake <= 0f) { s.t.localRotation = Quaternion.identity; s.t.localScale = s.scale; }
        }
    }

    private readonly List<(SpriteRenderer sr, RectInt pond, float speed, float phase)> shimmers = new List<(SpriteRenderer, RectInt, float, float)>();
    private readonly List<RectInt> waterPonds = new List<RectInt>();
    private float nextRipple;

    private void WaterShimmer(RectInt r)
    {
        waterPonds.Add(r);
        int count = Mathf.Clamp(r.width * r.height / 6, 2, 6);
        for (int i = 0; i < count; i++)
        {
            var sr = new GameObject("Shimmer").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = Shapes.Square;
            sr.sortingOrder = OverlayOrder + 1;
            Lighting.Glow(sr);
            sr.transform.localPosition = new Vector2(r.xMin + 1f + R() * (r.width - 2f), r.yMin + 1f + R() * (r.height - 2f));
            sr.transform.localScale = new Vector3(0.5f + R() * 0.8f, 0.07f, 1f);
            shimmers.Add((sr, r, 0.15f + R() * 0.25f, R() * 6f));
        }
    }

    private void UpdateWater(float dt, Vector2? walker)
    {
        float t = Time.unscaledTime;
        foreach (var (sr, pond, speed, phase) in shimmers)
        {
            Vector3 p = sr.transform.localPosition;
            p.x += speed * dt;
            if (p.x > pond.xMax - 1.2f) p.x = pond.xMin + 0.6f;
            sr.transform.localPosition = p;
            float a = 0.15f + Mathf.Max(0f, Mathf.Sin(t * 1.3f + phase)) * 0.4f;
            sr.color = new Color(0.85f, 0.95f, 1f, a);
        }
        if (walker == null || waterPonds.Count == 0 || Time.time < nextRipple) return;
        Vector2 w = walker.Value;
        foreach (RectInt r in waterPonds)
        {
            Vector2 edge = new Vector2(Mathf.Clamp(w.x, r.xMin + 0.3f, r.xMax - 0.3f), Mathf.Clamp(w.y, r.yMin + 0.3f, r.yMax - 0.3f));
            if ((edge - w).sqrMagnitude > 1.2f * 1.2f) continue;
            nextRipple = Time.time + 0.3f;
            ExplosionFx.Ring(edge, 0.45f, new Color(0.8f, 0.92f, 1f, 0.6f), 0.45f);
            return;
        }
    }

    private const int Pad = 2, Cell = 32 + 2 * Pad, CellsPerRow = 8;
    private Texture2D padded;
    private Color32[] paddedPx;
    private int nextCell;
    private readonly Dictionary<Texture2D, Color32[]> srcPixels = new Dictionary<Texture2D, Color32[]>();

    private Sprite Padded(Sprite s)
    {
        if (nextCell >= CellsPerRow * CellsPerRow) return s;
        Texture2D src = s.texture;
        if (!srcPixels.TryGetValue(src, out Color32[] px)) srcPixels[src] = px = ReadableCopy(src).GetPixels32();
        if (padded == null)
        {
            padded = new Texture2D(Cell * CellsPerRow, Cell * CellsPerRow, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            paddedPx = new Color32[padded.width * padded.height];
        }
        int sx = (int)s.rect.x, sy = (int)s.rect.y, w = (int)s.rect.width, h = (int)s.rect.height;
        int dx = nextCell % CellsPerRow * Cell, dy = nextCell / CellsPerRow * Cell;
        nextCell++;

        for (int y = -Pad; y < h + Pad; y++)
            for (int x = -Pad; x < w + Pad; x++)
                paddedPx[(dy + Pad + y) * padded.width + dx + Pad + x] =
                    px[(sy + Mathf.Clamp(y, 0, h - 1)) * src.width + sx + Mathf.Clamp(x, 0, w - 1)];
        padded.SetPixels32(paddedPx);
        padded.Apply(false);
        return Sprite.Create(padded, new Rect(dx + Pad, dy + Pad, w, h), new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private static Texture2D ReadableCopy(Texture2D src)
    {
        if (src.isReadable && src.format == TextureFormat.RGBA32) return src;
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply(false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    private static Color GroundTint(int x, int y)
    {
        float k = Mathf.PerlinNoise(x * 0.06f + 512f, y * 0.06f + 512f);
        float fine = Mathf.PerlinNoise(x * 0.25f + 77f, y * 0.25f + 77f);
        return Color.Lerp(new Color(0.84f, 0.88f, 0.8f), Color.white, Mathf.Clamp01(k * 1.2f - 0.1f + (fine - 0.5f) * 0.15f));
    }

    private readonly SpriteRenderer[] glow = new SpriteRenderer[4];
    private Transform player;
    private float nextEdgePopup;
    private const float GlowRange = 4f, GlowDepth = 1.6f;

    private void BuildGlow()
    {
        var tex = new Texture2D(1, 32) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int i = 0; i < 32; i++) tex.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.Pow(1f - i / 31f, 2f)));
        tex.Apply();
        Sprite fade = Sprite.Create(tex, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0f), 32f);

        float[] angles = { -90f, 90f, 0f, 180f };
        Vector2[] spots = { new Vector2(-halfSize, 0f), new Vector2(halfSize, 0f), new Vector2(0f, -halfSize), new Vector2(0f, halfSize) };
        for (int i = 0; i < 4; i++)
        {
            var sr = new GameObject("EdgeGlow").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = fade;
            sr.sortingOrder = GlowOrder;
            sr.transform.position = spots[i];
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angles[i]);
            sr.transform.localScale = new Vector3(halfSize * 64f, GlowDepth, 1f);
            sr.color = Color.clear;
            glow[i] = sr;
        }
    }

    private void Update()
    {
        for (int i = 0; i < lavaGlows.Count; i++)
            lavaGlows[i].color = new Color(1f, 0.45f, 0.1f, 0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 1.8f + i * 1.3f));
        foreach (var (sr, variant, phase) in lavaBlobs)
            sr.sprite = LavaArt.Get(variant, (int)((Time.unscaledTime + phase) * 5f), 1f);
        for (int i = 0; i < lavaLights.Count; i++)
            if (lavaLights[i] != null) lavaLights[i].intensity = 1.15f + 0.2f * Mathf.Sin(Time.unscaledTime * 3.1f + i * 1.7f) + 0.08f * Mathf.Sin(Time.unscaledTime * 11f + i);
        UpdateSway(Time.deltaTime);
        if (menuMap) { UpdateWater(Time.unscaledDeltaTime, null); return; }
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        Vector2 pos = player.position;
        if (Volcano && MainMenu.Started && Time.timeScale > 0f) VolcanoHazards(pos);
        UpdateWater(Time.unscaledDeltaTime, MainMenu.Started ? pos + Vector2.down * 0.3f : (Vector2?)null);
        float[] dist = { pos.x - Bounds.xMin, Bounds.xMax - pos.x, pos.y - Bounds.yMin, Bounds.yMax - pos.y };
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
        int touching = 0;
        for (int i = 0; i < 4; i++)
        {
            float k = Mathf.Clamp01(1f - (dist[i] - WallPad) / GlowRange);
            glow[i].color = new Color(1f, 0.4f, 0.25f, 0.7f * k * k * pulse);
            if (dist[i] < WallPad + 0.3f) touching++;
        }
        if (touching > 0 && Time.time >= nextEdgePopup)
        {
            nextEdgePopup = Time.time + 4f;
            DamagePopup.SpawnText(pos + Vector2.up * 1.4f, touching > 1 ? "CORNER OF THE MAP" : "EDGE OF THE MAP", new Color(1f, 0.6f, 0.4f), 4f);
        }
    }

    private float lavaTick, eruptionTimer = 20f, runClock;
    private PlayerHealth playerHealth;
    public const float LavaDamage = 4f;

    private void VolcanoHazards(Vector2 pos)
    {
        runClock += Time.deltaTime;
        if (playerHealth == null) playerHealth = player.GetComponent<PlayerHealth>();
        if (OnLava(pos) && (lavaTick -= Time.deltaTime) <= 0f)
        {
            lavaTick = 0.5f;
            if (playerHealth != null) playerHealth.TakeDamage(LavaDamage);
            Juice.SparksDir(pos + Vector2.down * 0.3f, Vector2.up, 40f, new Color(1f, 0.55f, 0.15f), 4, 3f, 0.1f);
        }
        if ((eruptionTimer -= Time.deltaTime) > 0f) return;
        eruptionTimer = Random.Range(7f, 11f);
        int count = 1 + Mathf.Min(2, (int)(runClock / 240f));
        for (int i = 0; i < count; i++)
        {
            Vector2 at = ClampInside(pos + Random.insideUnitCircle.normalized * Random.Range(i == 0 ? 1.5f : 3f, 7f), WallPad);
            DelayedBlast.Spawn(at, 1.6f, 1.4f, 10f, eruption: true);
        }
        Sfx.Play(Sfx.Id.Alert, 0.35f, 0.6f);
    }
}
