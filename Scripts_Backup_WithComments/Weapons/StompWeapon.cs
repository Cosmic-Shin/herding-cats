using UnityEngine;

// Ground slam: damages and shoves everything nearby away from you.
public class StompWeapon : Weapon
{
    private static readonly float[] Cooldown = { 3f, 3f, 2.8f, 2.6f, 2.4f, 2.2f, 2f };
    private static readonly float[] Radius = { 3f, 3.3f, 3.3f, 3.7f, 4f, 4f, 4.8f };
    private static readonly float[] Damage = { 16, 20, 23, 23, 28, 33, 42 };
    private const float Push = 10f;

    private void Update()
    {
        if (!Ready(Cooldown[L])) return;
        Sfx.Play(Sfx.Id.Boom, 0.45f, 0.6f);

        float radius = Area(Radius[L]);
        float damage = Dmg(Damage[L]);

        foreach (Collider2D c in EnemiesNear(Pos, radius))
        {
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, Pos, knockback: false);
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)c.transform.position - Pos).normalized * Push);
            if (c.TryGetComponent(out EnemyAI cat)) cat.Stun(0.4f);
        }

        StompFx.Spawn(Pos, radius); // cracked crater, dust shockwave, flying earth chunks
        for (int i = 0; i < 8; i++) // dust kicked up around the edge
            Juice.CatPuff(Pos + Rotate(Vector2.right, i * 45f + 20f) * radius * 0.75f, new Color(0.8f, 0.7f, 0.55f));
        if (TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(1.3f, 0.7f), 0.2f);
        CameraFollow.Shake(0.06f, 0.1f);
    }
}
