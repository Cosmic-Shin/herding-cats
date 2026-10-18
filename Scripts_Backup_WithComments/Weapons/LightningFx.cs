using System.Collections.Generic;
using UnityEngine;

// Jagged bolt from the sky down to a point, built from a few thin segments. Pooled.
public class LightningFx : MonoBehaviour
{
    private const int Segments = 5;
    private const float Duration = 0.18f;

    private static readonly Stack<LightningFx> free = new Stack<LightningFx>();
    private readonly SpriteRenderer[] parts = new SpriteRenderer[Segments];
    private readonly SpriteRenderer[] glows = new SpriteRenderer[Segments];
    private float t;
    private Color core = new Color(1f, 1f, 0.85f), halo = new Color(1f, 0.9f, 0.3f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => free.Clear();

    public static void Spawn(Vector2 target, Color? color = null, float width = 1f) =>
        SpawnBetween(target + new Vector2(Random.Range(-1f, 1f), 7f), target, color, width);

    // Jagged arc between any two points (Tesla Coils zaps).
    // color: null = classic yellow-white; otherwise the halo takes the color and the core a pale version of it.
    public static void SpawnBetween(Vector2 from, Vector2 target, Color? color = null, float width = 1f)
    {
        LightningFx fx = null;
        while (free.Count > 0 && fx == null) fx = free.Pop();
        if (fx != null) fx.gameObject.SetActive(true);
        else fx = Create();

        // zig-zag from 7 units above down to the target
        Vector2 prev = from;
        Vector2 side = Vector2.Perpendicular((target - from).normalized);
        for (int i = 0; i < Segments; i++)
        {
            Vector2 next = i == Segments - 1 ? target : Vector2.Lerp(prev, target, 1f / (Segments - i)) + side * Random.Range(-0.5f, 0.5f);
            Transform part = fx.parts[i].transform;
            Vector2 d = next - prev;
            part.position = prev;
            part.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            part.localScale = new Vector3(d.magnitude, 0.12f * width, 1f);
            prev = next;
        }
        fx.t = 0f;
        fx.halo = color ?? new Color(1f, 0.9f, 0.3f);
        fx.core = Color.Lerp(fx.halo, Color.white, 0.75f);
        Juice.Sparks(target, Color.Lerp(fx.halo, Color.white, 0.4f), 5, 5f, 0.12f);
    }

    private static LightningFx Create()
    {
        var root = new GameObject("Lightning");
        var fx = root.AddComponent<LightningFx>();
        for (int i = 0; i < Segments; i++)
        {
            var part = new GameObject("Bolt");
            part.transform.SetParent(root.transform, false);
            var sr = part.AddComponent<SpriteRenderer>();
            Lighting.Glow(sr);
            sr.sprite = Shapes.Square;
            sr.sortingOrder = 60;
            fx.parts[i] = sr;
            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>(); // wide soft halo around the bolt
            Lighting.Glow(glow);
            glow.transform.SetParent(part.transform, false);
            glow.transform.localScale = new Vector3(1f, 4f, 1f);
            glow.sprite = Shapes.Square;
            glow.sortingOrder = 59;
            fx.glows[i] = glow;
        }
        return fx;
    }

    private void Update()
    {
        t += Time.deltaTime / Duration;
        float a = (1f - t) * (Random.value < 0.25f ? 0.5f : 1f); // flickers as it fades
        for (int i = 0; i < Segments; i++)
        {
            parts[i].color = new Color(core.r, core.g, core.b, a);
            glows[i].color = new Color(halo.r, halo.g, halo.b, a * 0.35f);
        }
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            free.Push(this);
        }
    }
}
