using UnityEngine;

// Hurtful ground zone: toxic spit puddles, Queen Bee honey (slows) and pollen clouds.
public class EnemyPuddle : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const float FadeTime = 0.4f;
    private static readonly Color Pollen = new Color(1f, 0.88f, 0.3f);
    private static float lastCloudHit; // overlapping pollen clouds share one damage tick

    private float radius, damage, life, tickTimer, age, slow;
    private int variant, palette;
    private bool cloud;
    private SpriteRenderer sr;
    private PlayerHealth target;
    private PlayerController mover;

    public static void Spawn(Vector2 pos, float radius, float duration, float damage, PlayerHealth target,
                             int palette = LavaArt.ToxicPal, float slow = 0f, bool cloud = false)
    {
        var go = new GameObject(cloud ? "PollenCloud" : "Puddle");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        var p = go.AddComponent<EnemyPuddle>();
        p.sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(p.sr);
        p.sr.sortingOrder = cloud ? 3 : 2;
        p.radius = radius;
        p.damage = damage;
        p.life = duration;
        p.target = target;
        p.mover = target != null ? target.GetComponent<PlayerController>() : null;
        p.palette = palette;
        p.slow = slow;
        p.cloud = cloud;
        p.variant = Random.Range(0, LavaArt.Variants);
        if (cloud) p.sr.sprite = Shapes.Cloud;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        tickTimer -= Time.deltaTime;
        age += Time.deltaTime;

        float fade = Mathf.Clamp01(life / FadeTime), grow = Mathf.Clamp01(age / 0.15f);
        float size = radius * 2.4f * Mathf.Lerp(0.5f, 1f, grow) * Mathf.Lerp(0.7f, 1f, fade);
        if (cloud)
        {
            transform.localScale = Vector3.one * size * (1f + 0.06f * Mathf.Sin(age * 3f + variant));
            sr.color = new Color(Pollen.r, Pollen.g, Pollen.b, 0.55f * fade * Settings.EffectAlpha);
        }
        else
        {
            sr.sprite = LavaArt.Get(variant, (int)(age * 6f), radius, palette);
            transform.localScale = Vector3.one * size;
            sr.color = new Color(1f, 1f, 1f, 0.85f * fade * Settings.EffectAlpha);
        }
        if (!Settings.LowParticles && Random.value < Time.deltaTime * 3f)
            Juice.SparksDir((Vector2)transform.position + Random.insideUnitCircle * radius * 0.6f, Vector2.up, 20f, SparkColor, 1, 1.2f, 0.08f);

        bool inside = target != null && Vector2.Distance(target.transform.position, transform.position) <= radius;
        if (inside && slow > 0f && mover != null) mover.Slow(slow, 0.1f);
        if (tickTimer <= 0f)
        {
            tickTimer = TickInterval;
            if (inside && damage > 0f && (!cloud || Mathf.Abs(Time.time - lastCloudHit) >= TickInterval))
            {
                if (cloud) lastCloudHit = Time.time;
                target.TakeDamage(damage);
            }
        }

        if (life <= 0f) Destroy(gameObject);
    }

    private Color SparkColor => cloud ? new Color(1f, 0.95f, 0.5f, 0.8f)
        : palette == LavaArt.HoneyPal ? new Color(1f, 0.8f, 0.3f, 0.8f) : new Color(0.7f, 1f, 0.2f, 0.8f);
}
