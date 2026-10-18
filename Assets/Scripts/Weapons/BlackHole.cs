using UnityEngine;

public class BlackHole : MonoBehaviour
{
    private const float PullSpeed = 4.5f;
    private const int MoteCount = 18;
    private const float OpenTime = 0.3f;
    private const float CollapseTime = 0.2f;
    private const float ArtFill = 0.5f;
    private static readonly Color Purple = new Color(0.7f, 0.35f, 1f);

    private BlackHoleWeapon owner;
    private float pullRadius, duration, popRadius, popDamage, t;
    private SpriteRenderer core;
    public float PullRadius => pullRadius;
    public void Tint(Color color) => core.color = color;
    private readonly Transform[] motes = new Transform[MoteCount];
    private readonly Vector2[] moteState = new Vector2[MoteCount];

    public static BlackHole Open(Vector2 at, float pullRadius, float duration, float popRadius, float popDamage, BlackHoleWeapon owner)
    {
        var go = new GameObject("BlackHole");
        go.transform.position = at;
        var hole = go.AddComponent<BlackHole>();
        hole.owner = owner;
        hole.pullRadius = pullRadius;
        hole.duration = duration;
        hole.popRadius = popRadius;
        hole.popDamage = popDamage;
        hole.core = Layer(go.transform, Shapes.Art("blackhole"), Color.white, 1, 0f);
        for (int i = 0; i < MoteCount; i++)
        {
            Color c = Color.Lerp(Purple, Color.white, Random.Range(0f, 0.6f));
            hole.motes[i] = Layer(go.transform, Shapes.Circle, c, 3, Random.Range(0.08f, 0.16f)).transform;
            hole.moteState[i] = new Vector2(Random.Range(0f, 360f), Random.Range(0.3f, 1f) * pullRadius);
        }
        Juice.Sparks(at, Purple, 10, 5f, 0.15f);
        return hole;
    }

    private static SpriteRenderer Layer(Transform parent, Sprite sprite, Color color, int order, float size)
    {
        var go = new GameObject("Layer");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private static readonly System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>(64);

    private void Update()
    {
        t += Time.deltaTime;
        Vector2 center = transform.position;
        float k = Mathf.Clamp01(t / duration);

        float open = Mathf.SmoothStep(0f, 1f, t / OpenTime);
        float collapse = Mathf.SmoothStep(1f, 0.15f, (t - (duration - CollapseTime)) / CollapseTime);
        float throb = 1f + 0.04f * Mathf.Sin(t * 14f);
        core.transform.localScale = Vector3.one * pullRadius * 2f * ArtFill * open * collapse * throb;
        core.transform.Rotate(0f, 0f, (-120f - 480f * k * k) * Time.deltaTime);

        float maxR = pullRadius * collapse;
        for (int i = 0; i < MoteCount; i++)
        {
            Vector2 m = moteState[i];
            m.y -= (1.5f + 3f / Mathf.Max(0.3f, m.y)) * Time.deltaTime;
            m.x += 220f / Mathf.Max(0.5f, m.y) * Time.deltaTime;
            if (m.y < 0.25f) m = new Vector2(Random.Range(0f, 360f), maxR);
            m.y = Mathf.Min(m.y, maxR);
            moteState[i] = m;
            float a = m.x * Mathf.Deg2Rad;
            motes[i].localPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m.y;
        }

        foreach (Collider2D c in Bomb.OverlapEnemies(center, pullRadius, hits))
        {
            if (!c.TryGetComponent(out EnemyAI cat)) continue;
            Vector2 toCenter = center - (Vector2)c.transform.position;
            if (toCenter.sqrMagnitude > 0.04f) cat.Pull(toCenter.normalized * PullSpeed);
        }

        if (t >= duration)
        {
            Lighting.Flash(center, popRadius * 3f, Purple, 0.25f);
            ExplosionFx.Ring(center, popRadius * 0.8f, Color.white, 0.2f);
            ExplosionFx.Ring(center, popRadius * 1.3f, Purple);
            Juice.Sparks(center, Purple, 16, 10f, 0.2f);
            CameraFollow.Shake(0.05f, 0.1f);
            if (owner != null) owner.Pop(center, popRadius, popDamage);
            else Bomb.Blast(center, popRadius, popDamage, 0f, 1.5f, true);
            Destroy(gameObject);
        }
    }
}
