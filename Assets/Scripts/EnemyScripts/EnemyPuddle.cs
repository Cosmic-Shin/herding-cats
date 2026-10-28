using UnityEngine;

public class EnemyPuddle : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const float FadeTime = 0.4f;

    private float radius, damage, life, tickTimer, age;
    private int variant;
    private SpriteRenderer sr;
    private PlayerHealth target;

    public static void Spawn(Vector2 pos, float radius, float duration, float damage, PlayerHealth target)
    {
        var go = new GameObject("PoisonPuddle");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        var p = go.AddComponent<EnemyPuddle>();
        p.sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(p.sr);
        p.sr.sortingOrder = 2;
        p.radius = radius;
        p.damage = damage;
        p.life = duration;
        p.target = target;
        p.variant = Random.Range(0, LavaArt.Variants);
    }

    private void Update()
    {
        life -= Time.deltaTime;
        tickTimer -= Time.deltaTime;
        age += Time.deltaTime;

        sr.sprite = LavaArt.Get(variant, (int)(age * 6f), radius, LavaArt.ToxicPal);
        float fade = Mathf.Clamp01(life / FadeTime), grow = Mathf.Clamp01(age / 0.15f);
        transform.localScale = Vector3.one * radius * 2.4f * Mathf.Lerp(0.5f, 1f, grow) * Mathf.Lerp(0.7f, 1f, fade);
        sr.color = new Color(1f, 1f, 1f, 0.85f * fade * Settings.EffectAlpha);
        if (!Settings.LowParticles && Random.value < Time.deltaTime * 3f)
            Juice.SparksDir((Vector2)transform.position + Random.insideUnitCircle * radius * 0.6f, Vector2.up, 20f, new Color(0.7f, 1f, 0.2f, 0.8f), 1, 1.2f, 0.08f);

        if (tickTimer <= 0f)
        {
            tickTimer = TickInterval;
            if (target != null && Vector2.Distance(target.transform.position, transform.position) <= radius) target.TakeDamage(damage);
        }

        if (life <= 0f) Destroy(gameObject);
    }
}
