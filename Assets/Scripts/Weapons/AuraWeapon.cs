using UnityEngine;

public class AuraWeapon : Weapon
{
    private static readonly float[] Radius = { 2.2f, 2.5f, 2.5f, 2.8f, 3.1f, 3.1f, 3.8f };
    private static readonly float[] Damage = { 5, 5, 6, 6, 8, 9, 12 };
    private static readonly float[] Slow = { 0.15f, 0.15f, 0.15f, 0.2f, 0.2f, 0.25f, 0.35f };
    private static readonly Color Glow = new Color(0.65f, 0.6f, 1f);

    private SpriteRenderer fill, ripple;
    private float beat;

    protected override void Awake()
    {
        base.Awake();
        fill = MakeLayer(Field, new Color(1f, 1f, 1f, 0.55f), 3);
        ripple = MakeLayer(Shapes.Ring, Color.clear, 4);
    }

    private SpriteRenderer MakeLayer(Sprite sprite, Color color, int order)
    {
        var go = new GameObject("Aura");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private static AuraWeapon current;

    public static float ContactScale => current != null && PlayerCollabs.Has("auraguard") ? 0.7f : 1f;

    private void OnEnable() { SetVisible(true); current = this; }
    private void OnDisable() { SetVisible(false); if (current == this) current = null; }

    private void SetVisible(bool visible)
    {
        if (fill != null) fill.gameObject.SetActive(visible);
        if (ripple != null) ripple.gameObject.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (fill != null) Destroy(fill.gameObject);
        if (ripple != null) Destroy(ripple.gameObject);
    }

    protected float CurrentRadius => Area(Radius[L]);
    protected virtual Color FillTint => new Color(0.72f, 0.66f, 1f);

    private static Sprite field;
    private static Sprite Field
    {
        get
        {
            if (field != null) return field;
            const int s = 160;
            var px = new Color[s * s];
            float c = (s - 1) / 2f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy) / (s / 2f);
                    if (d > 1f) continue;
                    float ang = Mathf.Atan2(dy, dx);
                    float a = 0.08f + 0.2f * d * d;
                    if (d > 0.55f && d < 0.88f && (x + y) % 2 == 0) a += 0.07f;
                    if (d > 0.74f && d < 0.79f && Mathf.Sin(ang * 16f) > 0.2f) a = 0.5f;
                    if (d > 0.9f) a = 0.45f;
                    if (d > 0.95f) a = 0.95f;
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            return field = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }

    protected virtual void Update()
    {
        float radius = Area(Radius[L]);
        beat = Mathf.MoveTowards(beat, 0f, Time.deltaTime * 6f);
        float size = radius * 2f * (1f + 0.02f * Mathf.Sin(Time.time * 6f) + 0.08f * beat);
        fill.transform.localScale = Vector3.one * size;
        fill.color = new Color(FillTint.r, FillTint.g, FillTint.b, 0.7f + 0.3f * beat);

        float r = Mathf.Repeat(Time.time * 2f, 1f);
        ripple.transform.localScale = Vector3.one * radius * 2f * r;
        ripple.color = new Color(Glow.r, Glow.g, Glow.b, 0.45f * (1f - r));
        if (Random.value < 0.4f) Juice.Sparks(Pos + Random.insideUnitCircle * radius, new Color(0.75f, 0.8f, 1f, 0.8f), 1, 0.5f, 0.1f);

        if (!Ready(0.5f)) return;

        beat = 1f;
        float damage = Dmg(Damage[L]);
        int inside = 0;
        foreach (Collider2D c in EnemiesNear(Pos, radius))
        {
            if (c.TryGetComponent(out EnemyHealth e)) { Hit(e, damage, Pos, knockback: false); inside++; }
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Slow(Slow[L], 0.6f);
            Juice.Sparks(c.transform.position, Glow, 1, 2f, 0.1f);
        }
        if (inside > 0 && PlayerCollabs.Has("sanctuary") && stats.currentHealth < stats.maxHealth)
        {
            stats.Heal(Mathf.Min(3f, 0.3f * inside));
            Juice.Sparks(Pos, new Color(0.5f, 1f, 0.5f), 2, 1.5f, 0.12f);
        }
    }
}
