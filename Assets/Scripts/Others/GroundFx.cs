using UnityEngine;

public class GroundFx : MonoBehaviour
{
    private const int Max = 96, MaxPuddles = 12;
    private const float DryTime = 40f; // seconds for the ground to dry after a storm
    private static readonly Color PuddleColor = new Color(0.33f, 0.48f, 0.68f, 0.5f), Splash = new Color(0.75f, 0.88f, 1f);
    private static GroundFx instance;
    private static Sprite printSprite;

    private readonly SpriteRenderer[] prints = new SpriteRenderer[Max];
    private readonly float[] life = new float[Max], age = new float[Max], alpha = new float[Max];
    private int next;

    private readonly SpriteRenderer[] puddles = new SpriteRenderer[MaxPuddles];
    private readonly Vector2[] puddleSize = new Vector2[MaxPuddles];
    private readonly float[] puddleAge = new float[MaxPuddles];
    private int nextPuddle;
    private float wet, puddleTimer;

    private static float Storm => EnemySpawner.Endless ? 0f : Ambience.WeatherLevel;
    private static bool Rainy => !MapGenerator.Winter && !MapGenerator.Volcano;

    public static void Step(Vector2 feet, Vector2 facing, bool left)
    {
        if (MapGenerator.OnLava(feet)) return;
        if (instance == null) instance = new GameObject("GroundFx").AddComponent<GroundFx>();
        Vector2 dir = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector2.down;
        Vector2 at = feet + Vector2.Perpendicular(dir) * (left ? 0.07f : -0.07f);
        float wet = instance.wet, last = 1f + 2f * wet; // storms leave deeper prints that last longer

        if (MapGenerator.Winter)
        {
            instance.Print(at, dir, new Color(0.5f, 0.58f, 0.75f), Mathf.Min(1f, 0.6f + 0.25f * wet), 6f * last);
            Juice.Sparks(feet, Color.white, 2, 1.2f, 0.06f);
        }
        else if (MapGenerator.Volcano)
        {
            instance.Print(at, dir, new Color(0.12f, 0.1f, 0.1f), Mathf.Min(1f, 0.5f + 0.3f * wet), 3.5f * last);
            Juice.Sparks(feet, new Color(0.45f, 0.4f, 0.38f), 2, 1.4f, 0.09f);
        }
        else
        {
            Color mud = Color.Lerp(new Color(0.22f, 0.38f, 0.14f), new Color(0.2f, 0.17f, 0.11f), wet);
            instance.Print(at, dir, mud, 0.3f + 0.3f * wet, 1.5f * last);
            if (instance.InPuddle(feet)) Juice.SparksDir(feet, Vector2.up, 70f, Splash, 5, 2.6f, 0.08f);
            else Juice.SparksDir(feet, Vector2.up, 55f, new Color(0.45f, 0.75f, 0.3f), 2, 1.8f, 0.07f);
        }
    }

    private bool InPuddle(Vector2 p)
    {
        for (int i = 0; i < MaxPuddles; i++)
        {
            SpriteRenderer sr = puddles[i];
            if (sr == null || !sr.gameObject.activeSelf) continue;
            Vector2 d = p - (Vector2)sr.transform.position, half = sr.transform.localScale * 0.5f;
            if ((d.x * d.x) / (half.x * half.x) + (d.y * d.y) / (half.y * half.y) <= 1f) return true;
        }
        return false;
    }

    // Rain leaves puddles around the player that ripple while it rains and dry up afterwards.
    private void UpdatePuddles(float dt, float storm)
    {
        Camera cam = Camera.main;
        if (storm > 0.3f && cam != null && (puddleTimer -= dt) <= 0f)
        {
            puddleTimer = 1.2f;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 p = (Vector2)cam.transform.position + new Vector2(Random.Range(-w, w), Random.Range(-h, h));
            if (!MapGenerator.Blocked(p, 0.9f) && !MapGenerator.OnPond(p)) AddPuddle(p);
        }

        for (int i = 0; i < MaxPuddles; i++)
        {
            SpriteRenderer sr = puddles[i];
            if (sr == null || !sr.gameObject.activeSelf) continue;
            if (wet <= 0.01f) { sr.gameObject.SetActive(false); continue; }
            puddleAge[i] += dt;
            float dry = Mathf.Clamp01(wet * 1.5f);
            sr.transform.localScale = puddleSize[i] * Mathf.Clamp01(puddleAge[i] / 3f) * (0.4f + 0.6f * dry);
            sr.color = new Color(PuddleColor.r, PuddleColor.g, PuddleColor.b, PuddleColor.a * dry);
            if (storm > 0.3f && Random.value < dt * storm * 3f && !Settings.LowParticles)
                Ripple((Vector2)sr.transform.position + Vector2.Scale(Random.insideUnitCircle, sr.transform.localScale * 0.35f));
        }
    }

    private void AddPuddle(Vector2 at)
    {
        int i = nextPuddle;
        nextPuddle = (nextPuddle + 1) % MaxPuddles;
        if (puddles[i] == null)
        {
            puddles[i] = new GameObject("Puddle").AddComponent<SpriteRenderer>();
            puddles[i].transform.SetParent(transform, false);
            puddles[i].sprite = ExplosionFx.GetCircle();
            puddles[i].sortingOrder = -96;
        }
        puddles[i].transform.position = at;
        puddles[i].gameObject.SetActive(true);
        float size = Random.Range(1f, 2.2f);
        puddleSize[i] = new Vector2(size, size * Random.Range(0.45f, 0.6f));
        puddleAge[i] = 0f;
    }

    private static void Ripple(Vector2 at)
    {
        const float life = 0.5f;
        Shot r = Shot.Spawn(Shapes.Ring, Splash, 0.05f, at, -95);
        r.life = life;
        r.onUpdate = s =>
        {
            float k = s.age / life;
            s.transform.localScale = new Vector3(0.45f, 0.25f, 1f) * (0.2f + k);
            s.Renderer.color = new Color(Splash.r, Splash.g, Splash.b, 0.7f * (1f - k));
        };
    }

    private void Print(Vector2 at, Vector2 dir, Color color, float a, float seconds)
    {
        int i = next;
        next = (next + 1) % Max;
        if (prints[i] == null)
        {
            prints[i] = new GameObject("Print").AddComponent<SpriteRenderer>();
            prints[i].transform.SetParent(transform, false);
            prints[i].sprite = PrintSprite;
            prints[i].sortingOrder = -95;
        }
        SpriteRenderer sr = prints[i];
        sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f));
        sr.color = new Color(color.r, color.g, color.b, a);
        sr.gameObject.SetActive(true);
        life[i] = seconds;
        age[i] = 0f;
        alpha[i] = a;
    }

    private void Update()
    {
        float storm = Storm;
        wet = storm >= wet ? storm : Mathf.Max(0f, wet - Time.deltaTime / DryTime);
        if (Rainy) UpdatePuddles(Time.deltaTime, storm);

        for (int i = 0; i < Max; i++)
        {
            SpriteRenderer sr = prints[i];
            if (sr == null || !sr.gameObject.activeSelf) continue;
            age[i] += Time.deltaTime;
            float k = age[i] / life[i];
            if (k >= 1f) { sr.gameObject.SetActive(false); continue; }
            Color c = sr.color;
            c.a = alpha[i] * (1f - k * k);
            sr.color = c;
        }
    }

    private static Sprite PrintSprite
    {
        get
        {
            if (printSprite != null) return printSprite;
            string[] rows = { ".#.", "###", "###", ".#." };
            var tex = new Texture2D(3, 4) { filterMode = FilterMode.Point };
            for (int y = 0; y < 4; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, 3 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            return printSprite = Sprite.Create(tex, new Rect(0, 0, 3, 4), new Vector2(0.5f, 0.5f), 20f);
        }
    }
}
