using UnityEngine;

public class PixelFx : MonoBehaviour
{
    private class P
    {
        public SpriteRenderer sr;
        public Vector2 vel;
        public float age, life, gravity, scale, grow, alpha;
        public bool unscaled;
    }

    private const int Max = 400;
    private static PixelFx instance;
    private readonly P[] ps = new P[Max];
    private int next;

    private static PixelFx I => instance != null ? instance : instance = new GameObject("PixelFx").AddComponent<PixelFx>();

    public static void Emit(Sprite sprite, Vector2 pos, Vector2 vel, Color color, float life,
                            float scale = 1f, float gravity = 0f, float grow = 1f, bool unscaled = false, int order = 56)
    {
        if (Settings.LowParticles && Random.value < 0.5f) return;
        PixelFx fx = I;
        P p = fx.ps[fx.next] ??= new P();
        fx.next = (fx.next + 1) % Max;
        if (p.sr == null)
        {
            p.sr = new GameObject("Px").AddComponent<SpriteRenderer>();
            p.sr.transform.SetParent(fx.transform, false);
            Lighting.Glow(p.sr);
        }
        p.sr.sprite = sprite;
        p.sr.sortingOrder = order;
        p.sr.transform.position = pos;
        p.sr.transform.localScale = Vector3.one * scale;
        p.sr.color = color;
        p.sr.enabled = true;
        p.vel = vel;
        p.age = 0f;
        p.life = life;
        p.gravity = gravity;
        p.scale = scale;
        p.grow = grow;
        p.alpha = color.a * Settings.EffectAlpha;
        p.unscaled = unscaled;
    }

    public static void Burst(Sprite sprite, Vector2 pos, Color color, float life, float scale, float grow, bool unscaled = false) =>
        Emit(sprite, pos, Vector2.zero, color, life, scale, 0f, grow, unscaled, 57);

    public static void Toss(Sprite sprite, Vector2 pos, Color[] colors, int count, float speed, float life, float scale,
                            float gravity = 9f, bool unscaled = false)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.5f, 1f);
            Emit(sprite, pos, v, colors[Random.Range(0, colors.Length)], life * Random.Range(0.8f, 1.2f), scale, gravity, 1f, unscaled);
        }
    }

    public static void Poof(Vector2 pos, Color color, int count, float size = 1f, bool unscaled = false)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Emit(Shapes.PixelPuff, pos + dir * 0.1f, dir * Random.Range(1f, 2.2f) * size, Color.Lerp(color, Color.white, Random.value * 0.3f),
                 Random.Range(0.25f, 0.4f), Random.Range(0.7f, 1f) * size, -1.5f, 1.5f, unscaled, 55);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
        for (int i = 0; i < Max; i++)
        {
            P p = ps[i];
            if (p == null || !p.sr.enabled) continue;
            float d = p.unscaled ? udt : dt;
            p.age += d;
            float k = p.age / p.life;
            if (k >= 1f) { p.sr.enabled = false; continue; }
            p.vel.y -= p.gravity * d;
            p.vel *= 1f - Mathf.Min(1f, 1.5f * d);
            Transform t = p.sr.transform;
            t.position += (Vector3)(p.vel * d);
            t.localScale = Vector3.one * p.scale * Mathf.Lerp(1f, p.grow, k);
            Color c = p.sr.color;
            c.a = p.alpha * Mathf.Clamp01((1f - k) / 0.4f);
            p.sr.color = c;
        }
    }
}
