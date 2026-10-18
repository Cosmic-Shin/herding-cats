using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// One fixed, walled map built from the Nature tileset when the scene loads (same seed = same map every run).
//   tiles:   grass with soft color patches, dirt clearings, pebbles, cobble patches, ponds (water + reeds, lily pads)
//   sprites: tufts / flowers / mushrooms, bushes, rocks, stumps and logs, trees in groves, a tree line along the wall
// Ponds, rocks, logs and tree trunks are solid for Mira (Obstacle layer; PlayerController slides along them).
// Cats ignore them: they chase in straight lines, so walls would just pile them up.
// Mira can't leave Bounds; the camera stops at the edges (CameraFollow) and the edge she's near glows.
public class MapGenerator : MonoBehaviour
{
    [Header("Size")]
    public int halfSize = 48;       // playable map: 2 x halfSize tiles square, centered on 0,0 (1 tile = 1 unit)
    public bool menuMap;            // a small backdrop for the title screen: no wall, glow or gameplay hooks; built where this object sits
    public int outerMargin = 3;     // ground drawn past the wall, only ever seen during screen shake
    public int seed = 1337;

    [Header("Ground tiles (Ground Tileset, cut 32x32)")]
    public Sprite grass;
    public Sprite[] clearing = new Sprite[4];    // 2x2 dirt clearing: TL, TR, BL, BR
    public Sprite[] dirtSquare = new Sprite[4];  // 2x2 small dirt square: TL, TR, BL, BR
    public Sprite[] pebbles;                     // see-through stone bits laid over the grass
    public Sprite[] cobbles;                     // dense see-through stones, for small cobble patches

    [Header("Ponds (Water Tileset, cut 32x32)")]
    public Sprite water;
    public Sprite[] pondCorners = new Sprite[4]; // TL, TR, BL, BR
    public Sprite pondTop, pondBottom, pondLeft, pondRight;
    public Sprite[] reeds, lilies, glints;
    public int ponds = 9;

    [Header("Props")]
    public Sprite[] decor;      // grass tufts and flowers (most of the scatter)
    public Sprite[] rareDecor;  // mushrooms, pebbles, twigs
    [Range(0f, 1f)] public float rareDecorShare = 0.25f;
    public Sprite[] bushes;     // walk-through
    public Sprite[] rocks;      // solid
    public Sprite[] logs;       // stumps and logs, solid
    public Sprite[] trees;      // solid trunk

    [Header("Density (per 16x16 tiles)")]
    public float decorPerBlock = 34f;
    public float bushesPerBlock = 2.5f;
    public float rocksPerBlock = 1.5f;
    public float logsPerBlock = 1f;
    public float maxTreesPerBlock = 5f;  // in the thickest groves; open meadows get none
    public float clearingChance = 0.5f;  // per block
    public float pebbleChance = 0.02f;   // per grass tile
    public float startAreaRadius = 5f;   // nothing solid where Mira spawns

    public static Rect Bounds { get; private set; }
    public static bool Active => instance != null;
    public static Vector2? MenuCenter { get; private set; } // where the title-screen camera looks (CameraFollow)
    public static int ObstacleMask { get; private set; } // everything solid: stops walking
    public static int TallMask { get; private set; }     // ponds and tree trunks: also stop a dash
    public const float WallPad = 1.2f; // how far inside Bounds Mira stops: at the border tree line, fully on screen
    private static MapGenerator instance;

    private const int GroundOrder = -100, OverlayOrder = -99, DecorOrder = -90, MediumOrder = -80, TreeOrder = -70, GlowOrder = 9;

    private Tilemap ground, overlay;
    private readonly Dictionary<Sprite, Tile> tiles = new Dictionary<Sprite, Tile>();
    private int obstacleLayer, tallLayer;
    private System.Random rng;
    private float R() => (float)rng.NextDouble();
    private T Pick<T>(T[] a) => a[rng.Next(a.Length)];

    // ---------- helpers for other systems ----------

    public static Vector2 ClampInside(Vector2 p, float pad = 0.5f)
    {
        if (instance == null) return p;
        Rect b = Bounds;
        return new Vector2(Mathf.Clamp(p.x, b.xMin + pad, b.xMax - pad), Mathf.Clamp(p.y, b.yMin + pad, b.yMax - pad));
    }

    public static bool Blocked(Vector2 p, float radius) =>
        instance != null && (Physics2D.OverlapCircle(p, radius, ObstacleMask) != null || ClampInside(p, Mathf.Max(radius, WallPad)) != p);

    // Where a drop (gem, burger, pickup, crate) should land: inside the wall and not in a pond or rock.
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

    // ---------- build ----------

    private void Awake()
    {
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
        for (int i = 0; i < 32; i++) // only Mira.s own casts see them
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
        if (instance == this) instance = null;
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
        int Index(int x, int y) => (x - min) + (y - min) * n; // world tile coords -> array
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

        // ponds: rectangles kept apart from each other, the wall and the start area
        var pondRects = new List<RectInt>();
        for (int attempt = 0; attempt < ponds * 30 && pondRects.Count < ponds; attempt++)
        {
            int w = rng.Next(3, 9), h = rng.Next(3, 7);
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
                    else if (!edge && lilies.Length > 0 && R() < 0.14f) overlayTiles[i] = TileFor(Pick(lilies));
                    else if (!edge && glints.Length > 0 && R() < 0.06f) overlayTiles[i] = TileFor(Pick(glints));
                }
            // solid inside the grassy rim
            var go = new GameObject("Pond");
            go.layer = tallLayer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(r.width - 0.5f, r.height - 0.5f);
        }

        // dirt clearings, pebbles and cobble patches, a few per 16x16 block, never touching water
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

        // ground goes in as one batch with its tint (a SetColor per tile is far slower)
        var changes = new TileChangeData[n * n];
        for (int i = 0; i < changes.Length; i++)
        {
            int x = min + i % n, y = min + i / n;
            changes[i] = new TileChangeData(new Vector3Int(x, y, 0), groundTiles[i], GroundTint(x, y), Matrix4x4.identity); // ponds too, so their grassy rim matches the meadow
        }
        ground.SetTiles(changes, true);
        var all = new BoundsInt(new Vector3Int(min, min, 0), new Vector3Int(n, n, 1));
        overlay.SetTilesBlock(all, overlayTiles);

        // props
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
            if (!WaterAt(p, 1)) Prop(props, Pick(bushes), p, MediumOrder);
        }
        void Solids(Sprite[] set, float perBlock, bool tree)
        {
            for (int i = 0, count = Mathf.RoundToInt(perBlock * blocks); i < count; i++)
            {
                Vector2 p = Spot(2.5f);
                if (tree && R() > Grove(p)) continue; // trees gather where the grove noise is thick
                if (!Clear(p, tree ? 2.2f : 1.6f)) continue;
                solids.Add(p);
                SpriteRenderer sr = Prop(props, Pick(set), p, tree ? TreeOrder + Mathf.RoundToInt(Mathf.Repeat(-p.y, 16f)) : MediumOrder);
                Solid(sr, tree);
            }
        }
        Solids(rocks, rocksPerBlock, false);
        Solids(logs, logsPerBlock, false);
        Solids(trees, maxTreesPerBlock, true);

        // the wall reads as a line of bushes and small trees just inside it (Mira is held in by Bounds)
        var smallTrees = trees.Length > 3 ? trees[3..] : trees;
        for (float s = -halfSize; s < halfSize; s += 1.4f + R() * 0.8f)
            foreach (Vector2 side in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                float inset = 0.4f + R() * 0.6f;
                Vector2 along = new Vector2(side.y, side.x);
                Vector2 p = side * (halfSize - inset) + along * (s + R() * 0.5f);
                bool tree = R() < 0.4f;
                Prop(props, tree ? Pick(smallTrees) : Pick(bushes), p, tree ? TreeOrder + Mathf.RoundToInt(Mathf.Repeat(-p.y, 16f)) : MediumOrder);
            }
    }

    private float Grove(Vector2 p) => Mathf.Clamp01((Mathf.PerlinNoise(p.x * 0.023f + seed % 1000, p.y * 0.023f + seed % 997) - 0.35f) / 0.5f);

    private SpriteRenderer Prop(Transform parent, Sprite sprite, Vector2 at, int order)
    {
        if (sprite == null) return null;
        var go = new GameObject(sprite.name);
        go.transform.SetParent(parent, false);
        // snap to the 32px pixel grid so props don't shimmer against the tiles
        go.transform.localPosition = new Vector3(Mathf.Round(at.x * 32f) / 32f, Mathf.Round(at.y * 32f) / 32f, 0f); // map-local (the menu map sits far away)
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        if (R() < 0.5f) go.transform.localScale = new Vector3(-1f, 1f, 1f); // mirror the object (not flipX) so colliders mirror with it
        return sr;
    }

    // Rocks, stumps and logs: their own outline (the sprite's physics shape), on the low layer a dash can hop.
    // Trees: a box over the bottom of the actual trunk (found from its brown pixels, since trunks lean and split),
    // on the tall layer that also stops a dash.
    private void Solid(SpriteRenderer sr, bool tree)
    {
        if (sr == null) return;
        if (!tree)
        {
            sr.gameObject.layer = obstacleLayer;
            sr.gameObject.AddComponent<PolygonCollider2D>(); // auto-filled from the sprite's outline
            return;
        }
        sr.gameObject.layer = tallLayer;
        Rect trunk = TrunkBase(sr.sprite);
        var b = sr.gameObject.AddComponent<BoxCollider2D>();
        b.size = trunk.size;
        b.offset = trunk.center;
    }

    private readonly Dictionary<Sprite, Rect> trunks = new Dictionary<Sprite, Rect>();

    // The lowest third of the trunk (roots included), in the sprite's local units. Brown = clearly more red than
    // green and blue: the canopy is green and the ground shadow is grey-blue. Needs Read/Write on the Trees sheet.
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
        for (int y = h / 2; y > bottom; y--) // highest brown row in the lower half
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
        Vector2 pivot = s.pivot; // pixels from the rect's bottom-left
        return trunks[s] = new Rect((minX - pivot.x) / ppu, (bottom - pivot.y) / ppu, (maxX - minX + 1) / ppu, (bandTop - bottom + 1) / ppu);
    }

    private Tile TileFor(Sprite s)
    {
        if (s == null) return null;
        if (!tiles.TryGetValue(s, out Tile t))
        {
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Padded(s);
            t.flags = TileFlags.None; // let the ground take a tint
            tiles[s] = t;
        }
        return t;
    }

    // Tiles cut straight from the sheet show hairline seams of their neighbors when the camera lands between
    // pixels. Each tile used is copied (on the GPU) into its own cell with its edge pixels repeated around it.
    private const int Pad = 2, Cell = 32 + 2 * Pad, CellsPerRow = 8;
    private Texture2D padded;
    private int nextCell;

    private Sprite Padded(Sprite s)
    {
        if (nextCell >= CellsPerRow * CellsPerRow) return s; // ponytail: 64 distinct tiles max; grow the texture if the map uses more
        Texture2D src = s.texture;
        if (padded == null)
            padded = new Texture2D(Cell * CellsPerRow, Cell * CellsPerRow, src.format, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        if (src.format != padded.format) return s;
        int sx = (int)s.rect.x, sy = (int)s.rect.y, w = (int)s.rect.width, h = (int)s.rect.height;
        int dx = nextCell % CellsPerRow * Cell, dy = nextCell / CellsPerRow * Cell;
        nextCell++;

        Graphics.CopyTexture(src, 0, 0, sx, sy, w, h, padded, 0, 0, dx + Pad, dy + Pad);
        for (int p = 0; p < Pad; p++)
        {
            Graphics.CopyTexture(src, 0, 0, sx, sy, w, 1, padded, 0, 0, dx + Pad, dy + p);                   // bottom
            Graphics.CopyTexture(src, 0, 0, sx, sy + h - 1, w, 1, padded, 0, 0, dx + Pad, dy + Pad + h + p);   // top
            Graphics.CopyTexture(src, 0, 0, sx, sy, 1, h, padded, 0, 0, dx + p, dy + Pad);                   // left
            Graphics.CopyTexture(src, 0, 0, sx + w - 1, sy, 1, h, padded, 0, 0, dx + Pad + w + p, dy + Pad); // right
            for (int q = 0; q < Pad; q++) // corners (a texture can't copy into itself, so from the source too)
            {
                Graphics.CopyTexture(src, 0, 0, sx, sy, 1, 1, padded, 0, 0, dx + p, dy + q);
                Graphics.CopyTexture(src, 0, 0, sx + w - 1, sy, 1, 1, padded, 0, 0, dx + Pad + w + p, dy + q);
                Graphics.CopyTexture(src, 0, 0, sx, sy + h - 1, 1, 1, padded, 0, 0, dx + p, dy + Pad + h + q);
                Graphics.CopyTexture(src, 0, 0, sx + w - 1, sy + h - 1, 1, 1, padded, 0, 0, dx + Pad + w + p, dy + Pad + h + q);
            }
        }
        // never call padded.Apply(): the pixels only exist on the GPU, Apply would upload a blank CPU copy over them
        return Sprite.Create(padded, new Rect(dx + Pad, dy + Pad, w, h), new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // Big soft patches of lighter and darker grass so the meadow isn't one flat color.
    private static Color GroundTint(int x, int y)
    {
        float k = Mathf.PerlinNoise(x * 0.06f + 512f, y * 0.06f + 512f);
        float fine = Mathf.PerlinNoise(x * 0.25f + 77f, y * 0.25f + 77f);
        return Color.Lerp(new Color(0.84f, 0.88f, 0.8f), Color.white, Mathf.Clamp01(k * 1.2f - 0.1f + (fine - 0.5f) * 0.15f));
    }

    // ---------- edge warning ----------
    // A warm glow rises off whichever wall Mira is near (two at a corner), and a popup when she touches it.

    private readonly SpriteRenderer[] glow = new SpriteRenderer[4]; // left, right, bottom, top
    private Transform player;
    private float nextEdgePopup;
    private const float GlowRange = 4f, GlowDepth = 1.6f;

    private void BuildGlow()
    {
        // soft gradient: solid at the wall, fading out toward the map
        var tex = new Texture2D(1, 32) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int i = 0; i < 32; i++) tex.SetPixel(0, i, new Color(1f, 1f, 1f, Mathf.Pow(1f - i / 31f, 2f)));
        tex.Apply();
        Sprite fade = Sprite.Create(tex, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0f), 32f); // 1 unit tall, pivot at the wall

        float[] angles = { -90f, 90f, 0f, 180f }; // the fade points into the map from each wall
        Vector2[] spots = { new Vector2(-halfSize, 0f), new Vector2(halfSize, 0f), new Vector2(0f, -halfSize), new Vector2(0f, halfSize) };
        for (int i = 0; i < 4; i++)
        {
            var sr = new GameObject("EdgeGlow").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = fade;
            sr.sortingOrder = GlowOrder;
            sr.transform.position = spots[i];
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angles[i]);
            sr.transform.localScale = new Vector3(halfSize * 64f, GlowDepth, 1f); // 1/32-unit wide texture stretched along the whole wall
            sr.color = Color.clear;
            glow[i] = sr;
        }
    }

    private void Update()
    {
        if (menuMap) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        Vector2 pos = player.position;
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
}
