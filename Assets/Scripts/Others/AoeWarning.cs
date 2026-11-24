using UnityEngine;

public class AoeWarning : MonoBehaviour
{
    // Above the cats (5) and weapon fx, below bosses (60): the danger zone must never be hidden by the horde.
    private const int Order = 55;
    private const float EdgeWidth = 0.09f;
    private static readonly Color Red = new Color(1f, 0.15f, 0.1f), Hot = new Color(1f, 0.95f, 0.85f);

    private Transform fill;
    private SpriteRenderer[] edges;
    private float duration;
    private float t;

    public static void Spawn(Vector2 pos, float radius, float duration)
    {
        var go = new GameObject("AoeWarning");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        SpriteRenderer outer = Layer(go.transform, ExplosionFx.GetCircle(), new Color(Red.r, Red.g, Red.b, 0.18f), 0);

        var w = go.AddComponent<AoeWarning>();
        w.fill = Layer(go.transform, outer.sprite, new Color(1f, 0.2f, 0.1f, 0.3f), 1).transform;
        w.fill.localScale = Vector3.zero;
        w.edges = new[] { Layer(go.transform, Shapes.Ring, Red, 2) };
        w.duration = Mathf.Max(0.05f, duration);
    }

    public static void SpawnLine(Vector2 from, Vector2 dir, float length, float width, float duration)
    {
        var go = new GameObject("LineWarning");
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(length, width, 1f);
        Layer(go.transform, Shapes.Square, new Color(Red.r, Red.g, Red.b, 0.18f), 0); // Square's pivot is its left edge = 'from'

        var w = go.AddComponent<AoeWarning>();
        w.fill = Layer(go.transform, Shapes.Square, new Color(1f, 0.2f, 0.1f, 0.3f), 1).transform;
        w.fill.localScale = new Vector3(0f, 1f, 1f);
        w.lengthwise = true;

        // Two crisp side edges, so the lane reads even on red ground.
        float thin = EdgeWidth / Mathf.Max(0.01f, width);
        w.edges = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            w.edges[i] = Layer(go.transform, Shapes.Square, Red, 2);
            w.edges[i].transform.localPosition = new Vector3(0f, i == 0 ? 0.5f : -0.5f, 0f);
            w.edges[i].transform.localScale = new Vector3(1f, thin, 1f);
        }
        w.duration = Mathf.Max(0.05f, duration);
    }

    private static SpriteRenderer Layer(Transform parent, Sprite sprite, Color color, int orderOffset)
    {
        var sr = new GameObject("Layer").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        Lighting.Glow(sr);
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = Order + orderOffset;
        return sr;
    }

    private bool lengthwise;

    private void Update()
    {
        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);
        fill.localScale = lengthwise ? new Vector3(k, 1f, 1f) : Vector3.one * k;

        // Pulse faster as it fills; the last 20% flashes near-white = "it hits NOW".
        float pulse = Mathf.Abs(Mathf.Sin(t * duration * Mathf.Lerp(8f, 22f, k)));
        Color edge = k > 0.8f ? Color.Lerp(Red, Hot, pulse) : Red;
        edge.a = 0.6f + 0.4f * pulse;
        foreach (SpriteRenderer e in edges) e.color = edge;
        if (t >= 1f) Destroy(gameObject);
    }
}
