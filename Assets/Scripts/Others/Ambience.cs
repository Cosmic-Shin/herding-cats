using UnityEngine;

// Background life around the camera: drifting leaves and dust, fireflies as the run gets late,
// and rising embers once endless mode turns the sky red. Added by Lighting.
public class Ambience : MonoBehaviour
{
    private enum Kind { Leaf, Dust, Firefly, Ember }

    private class Mote
    {
        public SpriteRenderer sr;
        public Kind kind;
        public Vector2 vel;
        public float age, life, spin, phase, alpha;
    }

    private const int Max = 48;
    private readonly Mote[] motes = new Mote[Max];
    private Camera cam;
    private EnemySpawner spawner;
    private float leafTimer, dustTimer, flyTimer;
    private static Sprite leaf;
    private Material defaultMat;

    private static readonly Color[] LeafColors =
    {
        new Color(0.45f, 0.75f, 0.3f), new Color(0.6f, 0.82f, 0.35f), new Color(0.95f, 0.7f, 0.25f), new Color(0.9f, 0.45f, 0.2f),
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
    }

    // Minutes into the run; fireflies fade in from 5:00 and are at full strength by 13:00.
    private float Minutes => spawner != null ? spawner.RunTime / 60f : 0f;
    private float Dusk => Mathf.Clamp01((Minutes - 5f) / 8f);

    private void Update()
    {
        if (cam == null) return;
        float dt = MainMenu.IsOpen ? Time.unscaledDeltaTime : Time.deltaTime; // leaves and dust drift behind the menu too
        if (dt <= 0f) return;
        float rate = Settings.LowParticles ? 0.5f : 1f;
        bool endless = EnemySpawner.Endless;

        if ((leafTimer -= dt * rate) <= 0f) { leafTimer = Random.Range(0.4f, 0.9f); Spawn(endless ? Kind.Ember : Kind.Leaf); }
        if ((dustTimer -= dt * rate) <= 0f) { dustTimer = Random.Range(0.3f, 0.6f); Spawn(Kind.Dust); }
        if (MainMenu.Started && Dusk > 0f && (flyTimer -= dt * rate * Dusk) <= 0f) { flyTimer = Random.Range(0.25f, 0.5f); Spawn(Kind.Firefly); }

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
                    t.position += (Vector3)(m.vel + new Vector2(Mathf.Sin(m.age * 2.5f + m.phase) * 0.6f, 0f)) * dt;
                    t.Rotate(0f, 0f, m.spin * dt);
                    break;
                case Kind.Ember:
                    t.position += (Vector3)(m.vel + new Vector2(Mathf.Sin(m.age * 3f + m.phase) * 0.3f, 0f)) * dt;
                    fade *= 0.6f + 0.4f * Mathf.Sin(m.age * 12f + m.phase);
                    break;
                case Kind.Dust:
                    t.position += (Vector3)m.vel * dt;
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

    private void Spawn(Kind kind)
    {
        Mote m = System.Array.Find(motes, x => !x.sr.gameObject.activeSelf);
        if (m == null) return;

        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        Vector2 inView = c + new Vector2(Random.Range(-w, w), Random.Range(-h, h));
        m.kind = kind;
        m.age = 0f;
        m.phase = Random.value * 10f;
        m.sr.transform.rotation = Quaternion.identity;
        m.sr.sharedMaterial = defaultMat;

        switch (kind)
        {
            case Kind.Leaf:
                m.sr.sprite = Leaf;
                m.sr.transform.position = c + new Vector2(Random.Range(-w * 1.1f, w * 0.6f), h + 0.5f);
                m.sr.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
                m.sr.color = LeafColors[Random.Range(0, LeafColors.Length)];
                m.vel = new Vector2(Random.Range(0.4f, 0.9f), Random.Range(-1.1f, -0.6f));
                m.spin = Random.Range(-160f, 160f);
                m.life = 2f * h / -m.vel.y + 1f;
                m.alpha = 0.85f;
                break;
            case Kind.Ember:
                m.sr.sprite = Shapes.Circle;
                m.sr.transform.position = c + new Vector2(Random.Range(-w, w), -h - 0.3f);
                m.sr.transform.localScale = Vector3.one * Random.Range(0.06f, 0.11f);
                m.sr.color = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.85f, 0.35f), Random.value);
                Lighting.Glow(m.sr);
                m.vel = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.5f));
                m.life = 2f * h / m.vel.y;
                m.alpha = 0.9f;
                break;
            case Kind.Dust:
                m.sr.sprite = Shapes.Circle;
                m.sr.transform.position = inView;
                m.sr.transform.localScale = Vector3.one * Random.Range(0.03f, 0.06f);
                m.sr.color = new Color(1f, 1f, 0.95f);
                m.vel = Random.insideUnitCircle * 0.25f + new Vector2(0.15f, 0.05f);
                m.life = Random.Range(3f, 5f);
                m.alpha = 0.35f;
                break;
            case Kind.Firefly:
                m.sr.sprite = Shapes.Circle;
                m.sr.transform.position = inView;
                m.sr.transform.localScale = Vector3.one * Random.Range(0.08f, 0.12f);
                m.sr.color = new Color(0.85f, 1f, 0.45f);
                Lighting.Glow(m.sr);
                m.vel = Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.6f);
                m.life = Random.Range(4f, 7f);
                m.alpha = 1f;
                break;
        }
        m.sr.gameObject.SetActive(true);
    }

    // 5x3 leaf with a darker midrib; white so the colour tints it.
    private static Sprite Leaf
    {
        get
        {
            if (leaf != null) return leaf;
            string[] rows = { ".##..", "#####", "..##." };
            var tex = new Texture2D(5, 3) { filterMode = FilterMode.Point };
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 5; x++)
                    tex.SetPixel(x, 2 - y, rows[y][x] == '#' ? (y == 1 ? new Color(0.75f, 0.75f, 0.75f) : Color.white) : Color.clear);
            tex.Apply();
            return leaf = Sprite.Create(tex, new Rect(0, 0, 5, 3), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
