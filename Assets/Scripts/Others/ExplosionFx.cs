using System.Collections.Generic;
using UnityEngine;

public class ExplosionFx : MonoBehaviour
{
    private static Sprite circle;
    private static readonly Stack<ExplosionFx> free = new Stack<ExplosionFx>();
    private static readonly Color RingColor = new Color(1f, 0.85f, 0.6f);

    private SpriteRenderer sr;
    private float size;
    private float duration;
    private float alpha;
    private float t;
    private Color? tint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => free.Clear();

    public static void Prewarm(int count)
    {
        var made = new List<ExplosionFx>(count);
        for (int i = 0; i < count; i++) made.Add(Get());
        foreach (ExplosionFx fx in made) fx.Release();
    }

    public static void Spawn(Vector2 pos, float radius, Color? tint = null, int order = 50)
    {
        if (tint != null)
        {
            Layer(pos, radius * 2f, GetCircle(), tint, 0.25f, 0.8f, order);
            return;
        }

        Layer(pos, radius * 1.6f, GetCircle(), new Color(0.06f, 0.04f, 0.1f), 1.5f, 0.3f, 0);
        Layer(pos, radius * 2f, GetCircle(), null, 0.3f, 0.85f, 50);
        Layer(pos, radius * 0.8f, GetCircle(), Color.white, 0.08f, 0.9f, 52);
        Layer(pos, radius * 2.6f, Shapes.Ring, RingColor, 0.3f, 0.7f, 51);
        Lighting.Flash(pos, radius * 3.5f, new Color(1f, 0.7f, 0.35f));
        int n = Mathf.Clamp(Mathf.RoundToInt(radius * 6f), 4, 14);
        Juice.Sparks(pos, new Color(1f, 0.9f, 0.4f), n, 8f + radius * 4f, 0.22f);
        Juice.Smoke(pos, radius, n / 2);
    }

    public static void Ring(Vector2 pos, float radius, Color color, float duration = 0.35f) =>
        Layer(pos, radius * 2f, Shapes.Ring, color, duration, 0.9f, 51);

    private static void Layer(Vector2 pos, float size, Sprite sprite, Color? tint, float duration, float alpha, int order)
    {
        ExplosionFx fx = Get();
        fx.transform.position = pos;
        fx.transform.localScale = Vector3.zero;
        fx.sr.sprite = sprite;
        fx.sr.sortingOrder = order;
        fx.size = size;
        fx.tint = tint;
        fx.duration = duration;
        fx.alpha = alpha;
        fx.t = 0f;
    }

    private static ExplosionFx Get()
    {
        while (free.Count > 0)
        {
            ExplosionFx fx = free.Pop();
            if (fx == null) continue;
            fx.gameObject.SetActive(true);
            return fx;
        }

        var go = new GameObject("Explosion");
        var created = go.AddComponent<ExplosionFx>();
        created.sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(created.sr);
        created.sr.sprite = GetCircle();
        created.sr.sortingOrder = 50;
        return created;
    }

    private void Release()
    {
        gameObject.SetActive(false);
        free.Push(this);
    }

    private void Update()
    {
        t += Time.deltaTime / duration;
        float s = Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t)) * size;
        transform.localScale = new Vector3(s, s, 1f);
        Color c = tint ?? new Color(1f, Mathf.Lerp(0.75f, 0.3f, t), Mathf.Lerp(0.2f, 0.05f, t));
        sr.color = new Color(c.r, c.g, c.b, alpha * (1f - t));
        if (t >= 1f) Release();
    }

    public static Sprite GetCircle()
    {
        if (circle != null) return circle;

        const int n = 64;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                tex.SetPixel(x, y, d <= n / 2f ? Color.white : Color.clear);
            }
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return circle;
    }
}
