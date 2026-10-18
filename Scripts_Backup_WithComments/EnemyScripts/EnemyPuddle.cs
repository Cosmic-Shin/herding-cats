using UnityEngine;

// Poison puddle left by a Toxic Spitter's spit: hurts the player while they stand in it.
// (The player's i-frames cap it at one hit per second.)
public class EnemyPuddle : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const float FadeTime = 0.4f;

    private float radius, damage, life, tickTimer;
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
        p.sr.sprite = ExplosionFx.GetCircle();
        p.sr.sortingOrder = 2; // on the ground
        p.radius = radius;
        p.damage = damage;
        p.life = duration;
        p.target = target;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        tickTimer -= Time.deltaTime;

        float bubble = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f + transform.position.x);
        sr.color = new Color(0.45f, 0.9f, 0.3f, 0.4f * bubble * Mathf.Clamp01(life / FadeTime));

        if (tickTimer <= 0f)
        {
            tickTimer = TickInterval;
            if (target != null && Vector2.Distance(target.transform.position, transform.position) <= radius) target.TakeDamage(damage);
        }

        if (life <= 0f) Destroy(gameObject);
    }
}
