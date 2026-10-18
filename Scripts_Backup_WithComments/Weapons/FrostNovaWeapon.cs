using UnityEngine;

// Periodic icy pulse: damages and slows everything nearby; at max level it freezes cats briefly.
public class FrostNovaWeapon : Weapon
{
    private static readonly float[] Cooldown = { 3f, 3f, 2.7f, 2.7f, 2.4f, 2.4f, 2f };
    private static readonly float[] Radius = { 3.5f, 3.5f, 4f, 4f, 4.5f, 4.5f, 5.5f };
    private static readonly float[] Damage = { 12, 15, 15, 19, 19, 24, 30 };
    private static readonly float[] SlowAmount = { 0.4f, 0.4f, 0.45f, 0.5f, 0.5f, 0.55f, 0.6f };
    private const float SlowTime = 3f;

    private void Update()
    {
        if (!Ready(Cooldown[L])) return;
        Sfx.Play(Sfx.Id.Shatter, 0.5f);

        float radius = Area(Radius[L]);
        float damage = Dmg(Damage[L]);
        bool freeze = Level >= MaxLevel;

        foreach (Collider2D c in EnemiesNear(Pos, radius))
        {
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, Pos, knockback: false);
            Juice.Sparks(c.transform.position, new Color(0.75f, 0.95f, 1f), 2, 2f, 0.12f); // frost on each cat
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Slow(SlowAmount[L], SlowTime);
            if (freeze && c.TryGetComponent(out EnemyAI cat)) cat.Stun(1.5f);
        }

        StompFx.Spawn(Pos, radius, ice: true); // frost veins over a frosted patch + icy shockwave ring
        Lighting.Flash(Pos, radius * 1.5f, new Color(0.6f, 0.85f, 1f));
        Juice.Sparks(Pos, new Color(0.7f, 0.92f, 1f), 10, 10f + radius * 2f, 0.12f);
        ShardBurst(Pos, radius, 12);
    }

    // ice shards.png flying out to the edge of the nova, pointing the way they fly, shrinking as they go
    public static void ShardBurst(Vector2 at, float radius, int count)
    {
        const float life = 0.35f, size = 0.55f;
        Sprite shard = Shapes.Art("ice shards");
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count + Random.Range(-10f, 10f);
            Vector2 dir = Rotate(Vector2.right, angle);
            Shot s = Shot.Spawn(shard, Color.white, size, at + dir * 0.4f, sortingOrder: 45);
            s.velocity = dir * radius / life * Random.Range(0.8f, 1f);
            s.life = life;
            s.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f); // art points up
            s.onUpdate = shot =>
            {
                float k = shot.age / life;
                shot.transform.localScale = Vector3.one * size * (1f - 0.5f * k);
                shot.Renderer.color = new Color(1f, 1f, 1f, 1f - k * k);
            };
        }
    }
}
