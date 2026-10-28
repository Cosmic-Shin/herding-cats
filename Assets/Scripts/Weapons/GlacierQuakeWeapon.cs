using UnityEngine;

public class GlacierQuakeWeapon : Weapon
{
    private const float Cooldown = 2f;
    private const float Radius = 5f;
    private const float Damage = 45f;
    private const float Push = 10f;
    private const float FreezeTime = 1.5f;

    private void Update()
    {
        if (!Ready(Cooldown)) return;
        Sfx.Play(Sfx.Id.Shatter, 0.6f, 0.8f);

        float radius = Area(Radius);
        float damage = Dmg(Damage);

        foreach (Collider2D c in EnemiesNear(Pos, radius))
        {
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, Pos, knockback: false);
            if (c.TryGetComponent(out IEnemyBrain brain))
            {
                brain.Knockback(((Vector2)c.transform.position - Pos).normalized * Push);
                brain.Slow(0.6f, 2f);
            }
            if (c.TryGetComponent(out EnemyAI cat)) cat.Freeze(FreezeTime);
        }

        StompFx.Spawn(Pos, radius, ice: true);
        FrostNovaWeapon.ShardBurst(Pos, radius, 16);
        for (int i = 0; i < 12; i++)
        {
            Vector2 dir = Rotate(Vector2.right, i * 30f + Random.Range(-10f, 10f));
            Spike(Pos + dir * radius * Random.Range(0.35f, 0.9f), i * 0.012f);
        }
        Lighting.Flash(Pos, radius * 1.5f, new Color(0.6f, 0.9f, 1f), 0.25f);
        CameraFollow.Shake(0.1f, 0.15f);
    }

    private static void Spike(Vector2 at, float delay)
    {
        const float life = 0.75f, size = 0.9f;
        Shot s = Shot.Spawn(Shapes.Projectile("Icons/glacier"), Color.white, 0f, at, 20);
        s.life = life + delay;
        s.onUpdate = shot =>
        {
            float t = shot.age - delay;
            if (t < 0f) { shot.transform.localScale = Vector3.zero; return; }
            float rise = Mathf.Clamp01(t / 0.08f), sink = Mathf.Clamp01((life - t) / 0.25f);
            shot.transform.localScale = new Vector3(size * (0.7f + 0.3f * rise), size * rise * (0.9f + 0.1f * sink) * (rise < 1f ? 1.2f : 1f), 1f);
            shot.Renderer.color = new Color(1f, 1f, 1f, sink);
        };
    }
}
