using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Background life around the camera. Added by Lighting.
// Meadow: falling leaves, petals, pollen, butterflies and dust; fireflies (with little lights) as the run gets late.
// Winter: small far snow, big crystal flakes and ground glints. Volcano: falling ash and rising embers.
// All: a breeze with gusts (wind streaks) and soft cloud shadows drifting over the ground.
// Weather (started by WorldEvents): Meadow rainstorm, Winter blizzard, Volcano ash storm.
// Endless mode turns the sky red and swaps falling things for rising embers.
public class Ambience : MonoBehaviour
{
    private enum Kind { Leaf, Dust, Firefly, Ember, Petal, Pollen, Butterfly, Snow, Flake, Glint, Streak, Ash, Rain, Splash }

    // Weather: WorldEvents ramps Level 0..1 and back; the kind follows the map.
    public static float WeatherLevel;
    public static string WeatherName => MapGenerator.Winter ? "BLIZZARD" : MapGenerator.Volcano ? "ASH STORM" : "RAINSTORM";

    private readonly SpriteRenderer[] clouds = new SpriteRenderer[5];
    private readonly float[] cloudSpeed = new float[5];
    private readonly Light2D[] flyLights = new Light2D[6];
    private Image haze;
    private float rainTimer, splashTimer, boltTimer = 3f;

    private class Mote
    {
        public SpriteRenderer sr;
        public Kind kind;
        public Vector2 vel;
        public float age, life, spin, phase, alpha, size;
    }

    private const int Max = 260;
    private readonly Mote[] motes = new Mote[Max];
    private Camera cam;
    private EnemySpawner spawner;
    private float leafTimer, dustTimer, flyTimer, petalTimer, pollenTimer, butterflyTimer = 3f, flakeTimer, glintTimer, streakTimer;
    private float wind, gust, gustTimer = 6f;
    private Material defaultMat;

    private static Sprite leaf, petal, flake, glint;
    private static Sprite[] butterfly;

    private static readonly Color[] LeafColors =
    {
        new Color(0.45f, 0.75f, 0.3f), new Color(0.6f, 0.82f, 0.35f), new Color(0.95f, 0.7f, 0.25f), new Color(0.9f, 0.45f, 0.2f),
    };
    private static readonly Color[] PetalColors =
    {
        new Color(1f, 0.75f, 0.85f), new Color(1f, 0.95f, 0.95f), new Color(1f, 0.9f, 0.45f), new Color(0.85f, 0.75f, 1f),
    };
    private static readonly Color[] WingColors =
    {
        new Color(1f, 0.6f, 0.2f), new Color(0.45f, 0.7f, 1f), new Color(1f, 0.9f, 0.3f), new Color(0.95f, 0.55f, 0.9f),
    };

    private void Start()
    {
        cam = GetComponent<Camera>();
        spawner = FindFirstObjectByType<EnemySpawner>();
        for (int i = 0; i < Max; i++)
        {
            var sr = new GameObject("Mote").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform.parent, false);
            sr.sortingOrder = 55;
            defaultMat = sr.sharedMaterial;
            sr.gameObject.SetActive(false);
            motes[i] = new Mote { sr = sr };
        }

        // Cloud shadows: big soft dark shapes sliding over everything on the ground.
        for (int i = 0; i < clouds.Length; i++)
        {
            clouds[i] = new GameObject("CloudShadow").AddComponent<SpriteRenderer>();
            clouds[i].transform.SetParent(transform.parent, false);
            clouds[i].sprite = Shapes.Cloud;
            clouds[i].sortingOrder = 45; // over cats and props, under explosions and the UI
            PlaceCloud(i, true);
        }
        for (int i = 0; i < flyLights.Length; i++)
        {
            flyLights[i] = Lighting.MakeLight("FireflyLight", transform.parent, 1.4f, 0f, new Color(0.85f, 1f, 0.45f));
            flyLights[i].enabled = false;
        }

        // Weather haze: a screen overlay that's thicker at the edges (rain gloom, blizzard whiteout, ash haze).
        var canvas = new GameObject("WeatherCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10; // over the world, under the HUD and menus
        haze = new GameObject("Haze").AddComponent<Image>();
        haze.transform.SetParent(canvas.transform, false);
        haze.rectTransform.anchorMin = Vector2.zero;
        haze.rectTransform.anchorMax = Vector2.one;
        haze.rectTransform.offsetMin = haze.rectTransform.offsetMax = Vector2.zero;
        haze.sprite = HazeSprite();
        haze.raycastTarget = false;
        haze.color = Color.clear;
        WeatherLevel = 0f;
    }

    private void PlaceCloud(int i, bool anywhere)
    {
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        float size = Random.Range(6f, 11f);
        clouds[i].transform.localScale = new Vector3(size, size * 0.55f, 1f);
        clouds[i].transform.position = c + new Vector2(anywhere ? Random.Range(-w, w) : -w - size, Random.Range(-h, h));
        cloudSpeed[i] = Random.Range(0.25f, 0.5f);
    }

    private static Sprite HazeSprite()
    {
        const int n = 128;
        var tex = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.1f, d)))));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    // Minutes into the run; fireflies fade in from 5:00 and are at full strength by 13:00.
    private float Minutes => spawner != null ? spawner.RunTime / 60f : 0f;
    private float Dusk => Mathf.Clamp01((Minutes - 5f) / 8f);

    private void Update()
    {
        if (cam == null) return;
        float dt = MainMenu.IsOpen ? Time.unscaledDeltaTime : Time.deltaTime; // the world keeps drifting behind the menu
        if (dt <= 0f) return;
        float rate = Settings.LowParticles ? 0.5f : 1f;
        bool endless = EnemySpawner.Endless, winter = MapGenerator.Winter, volcano = MapGenerator.Volcano;

        // Breeze with the odd gust: ramps up and back down over ~3s.
        if ((gustTimer -= dt) <= 0f) { gustTimer = Random.Range(8f, 15f); gust = 3f; }
        gust = Mathf.Max(0f, gust - dt);
        float gustK = Mathf.Sin(Mathf.Clamp01(1f - gust / 3f) * Mathf.PI) * (gust > 0f ? 1f : 0f);
        float storm = endless ? 0f : WeatherLevel;
        wind = 0.3f + 2.2f * gustK + storm * (winter ? 5f : volcano ? 2f : 1.2f);
        float busy = 1f + 2f * gustK; // gusts stir up more stuff
        UpdateClouds(dt, storm);
        UpdateWeather(dt, storm, winter, volcano);

        bool Tick(ref float timer, float min, float max, float speed = 1f)
        {
            if ((timer -= dt * rate * speed) > 0f) return false;
            timer = Random.Range(min, max);
            return true;
        }

        if (endless)
        {
            if (Tick(ref leafTimer, 0.4f, 0.9f)) Spawn(Kind.Ember);
        }
        else if (volcano)
        {
            if (Tick(ref leafTimer, 0.12f, 0.25f, busy * (1f + 4f * storm))) Spawn(Kind.Ash);
            if (Tick(ref flakeTimer, 0.2f, 0.45f, 1f + 3f * storm)) Spawn(Kind.Ember);
        }
        else if (winter)
        {
            if (Tick(ref leafTimer, 0.1f, 0.2f, busy * (1f + 5f * storm))) Spawn(Kind.Snow);
            if (Tick(ref flakeTimer, 0.35f, 0.7f, busy * (1f + 2f * storm))) Spawn(Kind.Flake);
            if (Tick(ref glintTimer, 0.12f, 0.3f)) Spawn(Kind.Glint);
        }
        else
        {
            if (Tick(ref leafTimer, 0.3f, 0.7f, busy)) Spawn(Kind.Leaf);
            if (Tick(ref petalTimer, 0.5f, 1f, busy)) Spawn(Kind.Petal);
            if (Tick(ref pollenTimer, 0.25f, 0.5f)) Spawn(Kind.Pollen);
            if (Tick(ref dustTimer, 0.3f, 0.6f)) Spawn(Kind.Dust);
            if (Tick(ref butterflyTimer, 4f, 8f) && Count(Kind.Butterfly) < 3) Spawn(Kind.Butterfly);
        }
        if (gustK > 0.3f && Tick(ref streakTimer, 0.05f, 0.12f)) Spawn(Kind.Streak);
        if (MainMenu.Started && Dusk > 0f && !winter && !volcano && Tick(ref flyTimer, 0.25f, 0.5f, Dusk)) Spawn(Kind.Firefly);

        Vector2 windV = new Vector2(wind, 0f);
        foreach (Mote m in motes)
        {
            if (!m.sr.gameObject.activeSelf) continue;
            m.age += dt;
            float k = m.age / m.life;
            if (k >= 1f) { m.sr.gameObject.SetActive(false); continue; }
            float fade = Mathf.Min(1f, k * 5f, (1f - k) * 4f);
            Transform t = m.sr.transform;

            switch (m.kind)
            {
                case Kind.Leaf:
                    t.position += (Vector3)(m.vel + windV + new Vector2(Mathf.Sin(m.age * 2.5f + m.phase) * 0.6f, 0f)) * dt;
                    t.Rotate(0f, 0f, m.spin * (1f + gustK) * dt);
                    break;
                case Kind.Petal: // flutters and flips
                    t.position += (Vector3)(m.vel + windV * 1.2f + new Vector2(Mathf.Sin(m.age * 3f + m.phase) * 0.5f, Mathf.Cos(m.age * 4f + m.phase) * 0.2f)) * dt;
                    t.localScale = new Vector3(m.size * Mathf.Cos(m.age * 5f + m.phase), m.size, 1f);
                    t.Rotate(0f, 0f, m.spin * dt);
                    break;
                case Kind.Ash:
                case Kind.Snow:
                    t.position += (Vector3)(m.vel + windV * 0.8f + new Vector2(Mathf.Sin(m.age * 1.5f + m.phase) * 0.25f, 0f)) * dt;
                    break;
                case Kind.Flake: // big crystal flakes up front: slower sway, slow spin
                    t.position += (Vector3)(m.vel + windV + new Vector2(Mathf.Sin(m.age * 1.2f + m.phase) * 0.4f, 0f)) * dt;
                    t.Rotate(0f, 0f, m.spin * dt);
                    break;
                case Kind.Glint: // twinkle on the snow
                    float tw = Mathf.Sin(k * Mathf.PI);
                    t.localScale = Vector3.one * m.size * tw;
                    fade = tw;
                    break;
                case Kind.Pollen:
                    m.vel = Quaternion.Euler(0f, 0f, Random.Range(-120f, 120f) * dt) * m.vel;
                    t.position += (Vector3)(m.vel + windV * 0.5f) * dt;
                    fade *= 0.6f + 0.4f * Mathf.Sin(m.age * 4f + m.phase);
                    break;
                case Kind.Butterfly: // crosses the screen in a bobbing line, wings flapping
                    t.position += (Vector3)(m.vel + new Vector2(0f, Mathf.Sin(m.age * 3f + m.phase) * 0.8f) + windV * 0.3f) * dt;
                    m.sr.sprite = Butterfly[(int)(m.age * 10f + m.phase) % 2];
                    break;
                case Kind.Rain:
                    t.position += (Vector3)m.vel * dt;
                    break;
                case Kind.Splash:
                    t.localScale = Vector3.one * m.size * Mathf.Sqrt(k);
                    fade = 1f - k;
                    break;
                case Kind.Streak:
                    t.position += (Vector3)(m.vel + windV * 2f) * dt;
                    break;
                case Kind.Ember:
                    t.position += (Vector3)(m.vel + new Vector2(Mathf.Sin(m.age * 3f + m.phase) * 0.3f, 0f)) * dt;
                    fade *= 0.6f + 0.4f * Mathf.Sin(m.age * 12f + m.phase);
                    break;
                case Kind.Dust:
                    t.position += (Vector3)(m.vel + windV * 0.4f) * dt;
                    break;
                case Kind.Firefly:
                    m.vel = Quaternion.Euler(0f, 0f, Random.Range(-200f, 200f) * dt) * m.vel; // lazy wander
                    t.position += (Vector3)m.vel * dt;
                    fade *= 0.35f + 0.65f * Mathf.Max(0f, Mathf.Sin(m.age * 3f + m.phase)); // blink
                    break;
            }
            Color c = m.sr.color;
            c.a = m.alpha * fade;
            m.sr.color = c;
        }
    }

    private void UpdateClouds(float dt, float storm)
    {
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        // Darker sky in a storm; a smoky brown on the volcano.
        Color tint = MapGenerator.Volcano ? new Color(0.12f, 0.05f, 0.03f) : new Color(0.05f, 0.06f, 0.12f);
        float a = (MapGenerator.Winter ? 0.1f : MapGenerator.Volcano ? 0.17f : 0.15f) * (1f + 1.2f * storm);
        for (int i = 0; i < clouds.Length; i++)
        {
            Transform t = clouds[i].transform;
            t.position += Vector3.right * (cloudSpeed[i] + wind * 0.3f) * dt;
            float size = t.localScale.x;
            Vector2 p = t.position;
            if (p.x - size > c.x + w || Mathf.Abs(p.y - c.y) > h + size) PlaceCloud(i, false); // drifted off: bring a new one in from the left
            clouds[i].color = new Color(tint.r, tint.g, tint.b, a);
        }
    }

    private void UpdateWeather(float dt, float storm, bool winter, bool volcano)
    {
        // Screen haze and daylight tint by weather.
        Color hazeColor = winter ? new Color(0.92f, 0.95f, 1f, 0.55f) : volcano ? new Color(0.35f, 0.12f, 0.05f, 0.4f) : new Color(0.12f, 0.15f, 0.22f, 0.35f);
        hazeColor.a *= storm * Settings.EffectAlpha;
        haze.color = hazeColor;
        Color tint = winter ? new Color(0.9f, 0.93f, 1f) : volcano ? new Color(1f, 0.6f, 0.5f) : new Color(0.65f, 0.7f, 0.85f);
        Lighting.WeatherTint = Color.Lerp(Color.white, tint, storm);

        if (storm > 0.05f && !winter && !volcano) // rain, splashes and lightning
        {
            float rate = Settings.LowParticles ? 0.5f : 1f;
            if ((rainTimer -= dt * rate * storm) <= 0f) { rainTimer = 0.012f; Spawn(Kind.Rain); }
            if ((splashTimer -= dt * rate * storm) <= 0f) { splashTimer = 0.06f; Spawn(Kind.Splash); }
            if (storm > 0.6f && MainMenu.Started && (boltTimer -= dt) <= 0f)
            {
                boltTimer = Random.Range(4f, 9f);
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 at = (Vector2)cam.transform.position + new Vector2(Random.Range(-w * 0.8f, w * 0.8f), Random.Range(-h * 0.6f, h * 0.6f));
                LightningFx.Spawn(at, new Color(0.85f, 0.9f, 1f), 1.4f);
                Lighting.Flash(at, 14f, new Color(0.8f, 0.85f, 1f), 0.25f);
                Juice.Flash(new Color(0.9f, 0.95f, 1f, 0.3f * Settings.FlashScale), 0.15f);
                Sfx.Play(Sfx.Id.Boom, 0.45f, 0.4f); // thunder
            }
        }

        // Fireflies carry small lights (up to 6).
        int n = 0;
        foreach (Mote m in motes)
        {
            if (n >= flyLights.Length) break;
            if (m.kind != Kind.Firefly || !m.sr.gameObject.activeSelf) continue;
            Light2D l = flyLights[n++];
            l.enabled = Settings.Lights;
            l.transform.position = m.sr.transform.position;
            l.intensity = 0.5f * m.sr.color.a;
        }
        for (; n < flyLights.Length; n++) flyLights[n].enabled = false;
    }

    private int Count(Kind kind)
    {
        int n = 0;
        foreach (Mote m in motes) if (m.kind == kind && m.sr.gameObject.activeSelf) n++;
        return n;
    }

    private void Spawn(Kind kind)
    {
        Mote m = System.Array.Find(motes, x => !x.sr.gameObject.activeSelf);
        if (m == null) return;

        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        Vector2 inView = c + new Vector2(Random.Range(-w, w), Random.Range(-h, h));
        Vector2 fromTop = c + new Vector2(Random.Range(-w * 1.3f, w * 0.7f), h + 0.5f); // skewed left: the wind blows right
        Transform t = m.sr.transform;
        m.kind = kind;
        m.age = 0f;
        m.phase = Random.value * 10f;
        m.spin = 0f;
        t.rotation = Quaternion.identity;
        m.sr.sharedMaterial = defaultMat;
        m.sr.flipX = false;
        m.sr.sortingOrder = 55;

        switch (kind)
        {
            case Kind.Leaf:
                m.sr.sprite = Leaf;
                t.position = fromTop;
                t.localScale = Vector3.one * Random.Range(1f, 1.5f);
                m.sr.color = LeafColors[Random.Range(0, LeafColors.Length)];
                m.vel = new Vector2(Random.Range(0.2f, 0.6f), Random.Range(-1.1f, -0.6f));
                m.spin = Random.Range(-160f, 160f);
                m.life = 2f * h / -m.vel.y + 1f;
                m.alpha = 0.85f;
                break;
            case Kind.Petal:
                m.sr.sprite = Petal;
                t.position = fromTop;
                m.size = Random.Range(1f, 1.5f);
                t.localScale = Vector3.one * m.size;
                m.sr.color = PetalColors[Random.Range(0, PetalColors.Length)];
                m.vel = new Vector2(Random.Range(0.1f, 0.4f), Random.Range(-0.7f, -0.4f));
                m.spin = Random.Range(-60f, 60f);
                m.life = 2f * h / -m.vel.y + 1f;
                m.alpha = 0.9f;
                break;
            case Kind.Pollen:
                m.sr.sprite = Shapes.Circle;
                t.position = inView;
                t.localScale = Vector3.one * Random.Range(0.05f, 0.08f);
                m.sr.color = new Color(1f, 0.95f, 0.6f);
                Lighting.Glow(m.sr);
                m.vel = new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.05f, 0.2f));
                m.life = Random.Range(4f, 7f);
                m.alpha = 0.6f;
                break;
            case Kind.Butterfly:
                bool right = Random.value < 0.5f;
                m.sr.sprite = Butterfly[0];
                m.sr.flipX = !right;
                t.position = c + new Vector2(right ? -w - 0.5f : w + 0.5f, Random.Range(-h * 0.7f, h * 0.7f));
                t.localScale = Vector3.one * 1.6f;
                m.sr.color = WingColors[Random.Range(0, WingColors.Length)];
                m.sr.sortingOrder = 56;
                m.vel = new Vector2((right ? 1f : -1f) * Random.Range(0.8f, 1.3f), Random.Range(-0.15f, 0.15f));
                m.life = (2f * w + 1f) / Mathf.Abs(m.vel.x);
                m.alpha = 0.95f;
                break;
            case Kind.Snow:
                m.sr.sprite = Shapes.Circle;
                t.position = fromTop;
                t.localScale = Vector3.one * Random.Range(0.05f, 0.1f);
                m.sr.color = new Color(0.95f, 0.97f, 1f);
                Lighting.Glow(m.sr); // unlit, so it stays brighter than the lit snow it falls over
                m.vel = new Vector2(Random.Range(0f, 0.2f), Random.Range(-0.7f, -0.4f));
                m.life = 2f * h / -m.vel.y + 1f;
                m.alpha = 0.7f;
                break;
            case Kind.Ash: // grey flakes drifting down from the smoke
                m.sr.sprite = Random.value < 0.3f ? Leaf : Shapes.Circle;
                t.position = fromTop;
                t.localScale = Vector3.one * (m.sr.sprite == Leaf ? Random.Range(0.6f, 1f) : Random.Range(0.04f, 0.09f));
                m.sr.color = Color.Lerp(new Color(0.25f, 0.23f, 0.23f), new Color(0.6f, 0.56f, 0.54f), Random.value);
                m.vel = new Vector2(Random.Range(0f, 0.25f), Random.Range(-0.6f, -0.3f));
                m.life = 2f * h / -m.vel.y + 1f;
                m.alpha = 0.7f;
                break;
            case Kind.Flake:
                m.sr.sprite = Flake;
                t.position = fromTop;
                t.localScale = Vector3.one * Random.Range(0.9f, 1.6f);
                m.sr.color = new Color(0.95f, 0.98f, 1f);
                Lighting.Glow(m.sr);
                m.sr.sortingOrder = 62; // in front of the action, so keep them sparse
                m.vel = new Vector2(Random.Range(0.1f, 0.3f), Random.Range(-1.3f, -0.9f));
                m.spin = Random.Range(-40f, 40f);
                m.life = 2f * h / -m.vel.y + 1f;
                m.alpha = 0.75f;
                break;
            case Kind.Glint:
                m.sr.sprite = Glint;
                t.position = inView;
                m.size = Random.Range(1f, 1.5f);
                t.localScale = Vector3.zero;
                m.sr.color = Random.value < 0.5f ? Color.white : new Color(0.7f, 0.92f, 1f);
                m.sr.sortingOrder = 4; // on the ground
                Lighting.Glow(m.sr);
                m.vel = Vector2.zero;
                m.life = Random.Range(0.5f, 1f);
                m.alpha = 0.9f;
                break;
            case Kind.Rain: // slanted streaks falling fast
                m.sr.sprite = Shapes.Square;
                m.vel = new Vector2(2f + wind * 0.6f, -13f);
                t.position = c + new Vector2(Random.Range(-w * 1.3f, w), h + 0.5f);
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(m.vel.y, m.vel.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(Random.Range(0.45f, 0.75f), 0.03f, 1f);
                m.sr.color = new Color(0.75f, 0.85f, 1f);
                m.sr.sortingOrder = 60;
                m.life = 2f * h / 13f + 0.3f;
                m.alpha = 0.45f;
                break;
            case Kind.Splash: // little ring where a drop lands
                m.sr.sprite = Shapes.Ring;
                t.position = inView;
                m.size = Random.Range(0.25f, 0.4f);
                t.localScale = Vector3.zero;
                m.sr.color = new Color(0.8f, 0.9f, 1f);
                m.sr.sortingOrder = 4;
                Lighting.Glow(m.sr);
                m.vel = Vector2.zero;
                m.life = 0.35f;
                m.alpha = 0.6f;
                break;
            case Kind.Streak:
                m.sr.sprite = Shapes.Circle;
                t.position = c + new Vector2(Random.Range(-w * 1.2f, w * 0.5f), Random.Range(-h, h));
                t.localScale = new Vector3(Random.Range(0.8f, 1.6f), 0.025f, 1f);
                m.sr.color = Color.white;
                m.vel = new Vector2(Random.Range(3f, 5f), 0f);
                m.life = Random.Range(0.4f, 0.7f);
                m.alpha = 0.22f;
                break;
            case Kind.Ember:
                m.sr.sprite = Shapes.Circle;
                t.position = c + new Vector2(Random.Range(-w, w), -h - 0.3f);
                t.localScale = Vector3.one * Random.Range(0.06f, 0.11f);
                m.sr.color = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.85f, 0.35f), Random.value);
                Lighting.Glow(m.sr);
                m.vel = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.5f));
                m.life = 2f * h / m.vel.y;
                m.alpha = 0.9f;
                break;
            case Kind.Dust:
                m.sr.sprite = Shapes.Circle;
                t.position = inView;
                t.localScale = Vector3.one * Random.Range(0.03f, 0.06f);
                m.sr.color = new Color(1f, 1f, 0.95f);
                m.vel = Random.insideUnitCircle * 0.25f + new Vector2(0.15f, 0.05f);
                m.life = Random.Range(3f, 5f);
                m.alpha = 0.35f;
                break;
            case Kind.Firefly:
                m.sr.sprite = Shapes.Circle;
                t.position = inView;
                t.localScale = Vector3.one * Random.Range(0.08f, 0.12f);
                m.sr.color = new Color(0.85f, 1f, 0.45f);
                Lighting.Glow(m.sr);
                m.vel = Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.6f);
                m.life = Random.Range(4f, 7f);
                m.alpha = 1f;
                break;
        }
        m.sr.gameObject.SetActive(true);
    }

    // Tiny pixel sprites: '#' white, '+' light grey (so the tint shows a darker detail line).
    private static Sprite Make(params string[] rows)
    {
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, rows[y][x] == '#' ? Color.white : rows[y][x] == '+' ? new Color(0.75f, 0.75f, 0.75f) : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 20f); // same pixel size as the hero art
    }

    public static Sprite Leaf => leaf != null ? leaf : leaf = Make(".##..", "+++++", "..##.");
    public static Sprite Petal => petal != null ? petal : petal = Make(".##", "##+", "##.");
    public static Sprite Flake => flake != null ? flake : flake = Make("...#...", ".#.#.#.", "..###..", "###+###", "..###..", ".#.#.#.", "...#...");
    private static Sprite Glint => glint != null ? glint : glint = Make(".#.", "###", ".#.");
    private static Sprite[] Butterfly => butterfly != null ? butterfly : butterfly = new[]
    {
        Make("##.##", "#####", ".#+#.", "##.##"), // wings open
        Make(".#.#.", ".###.", "..+..", ".#.#."), // wings up
    };
}
