using UnityEngine;

public class AoeWarning : MonoBehaviour
{
    private Transform fill;
    private float duration;
    private float t;

    public static void Spawn(Vector2 pos, float radius, float duration)
    {
        var go = new GameObject("AoeWarning");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        var outer = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(outer);
        outer.sprite = ExplosionFx.GetCircle();
        outer.color = new Color(1f, 0.15f, 0.1f, 0.25f);
        outer.sortingOrder = 2;

        var inner = new GameObject("Fill");
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.zero;
        var innerSr = inner.AddComponent<SpriteRenderer>();
        Lighting.Glow(innerSr);
        innerSr.sprite = outer.sprite;
        innerSr.color = new Color(1f, 0.2f, 0.1f, 0.35f);
        innerSr.sortingOrder = 3;

        var rim = new GameObject("Rim").AddComponent<SpriteRenderer>();
        rim.transform.SetParent(go.transform, false);
        Lighting.Glow(rim);
        rim.sprite = Shapes.Ring;
        rim.sortingOrder = 4;

        var w = go.AddComponent<AoeWarning>();
        w.fill = inner.transform;
        w.rim = rim;
        w.duration = Mathf.Max(0.05f, duration);
    }

    public static void SpawnLine(Vector2 from, Vector2 dir, float length, float width, float duration)
    {
        var go = new GameObject("LineWarning");
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(length, width, 1f);
        var outer = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(outer);
        outer.sprite = Shapes.Square;
        outer.color = new Color(1f, 0.15f, 0.1f, 0.25f);
        outer.sortingOrder = 2;

        var inner = new GameObject("Fill");
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = new Vector3(0f, 1f, 1f);
        var innerSr = inner.AddComponent<SpriteRenderer>();
        Lighting.Glow(innerSr);
        innerSr.sprite = outer.sprite;
        innerSr.color = new Color(1f, 0.2f, 0.1f, 0.35f);
        innerSr.sortingOrder = 3;

        var w = go.AddComponent<AoeWarning>();
        w.fill = inner.transform;
        w.duration = Mathf.Max(0.05f, duration);
        w.lengthwise = true;
    }

    private bool lengthwise;
    private SpriteRenderer rim;

    private void Update()
    {
        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);
        fill.localScale = lengthwise ? new Vector3(k, 1f, 1f) : Vector3.one * k;
        if (rim != null) rim.color = new Color(1f, 0.25f, 0.15f, 0.55f + 0.4f * Mathf.Abs(Mathf.Sin(t * duration * 12f)));
        if (t >= 1f) Destroy(gameObject);
    }
}
