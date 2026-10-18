using UnityEngine;

// Collab: Frost Nova + Shockwave Stomp. A huge icy slam that shoves everything back and
// freezes cats solid; frozen cats take 50% more damage from everything (EnemyHealth).
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
                brain.Slow(0.6f, 2f); // bosses can't be frozen, but they still get slowed
            }
            if (c.TryGetComponent(out EnemyAI cat)) cat.Freeze(FreezeTime);
        }

        StompFx.Spawn(Pos, radius);            // cracked ground, dust ring, flying earth
        StompFx.Spawn(Pos, radius, ice: true); // frost spreading over it
        FrostNovaWeapon.ShardBurst(Pos, radius, 16);
        CameraFollow.Shake(0.1f, 0.15f);
    }
}
