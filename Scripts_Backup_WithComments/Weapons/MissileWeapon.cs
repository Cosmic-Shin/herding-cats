using UnityEngine;

// Homing missiles that go for the toughest enemy nearby and explode on impact.
// Their blasts count as explosions, so Chain Reaction / Cluster Bomb / Napalm trigger.
public class MissileWeapon : Weapon
{
    private static readonly int[] Count = { 2, 3, 3, 4, 4, 5, 8 };
    private static readonly float[] Damage = { 16, 16, 20, 20, 25, 25, 32 };
    private static readonly float[] Radius = { 1.1f, 1.1f, 1.1f, 1.1f, 1.3f, 1.3f, 1.6f };
    private const float Speed = 8f;
    private const float TurnRate = 360f; // degrees / sec

    protected void Update() // protected: Cluster Barrage inherits it
    {
        if (!Ready(2f)) return;
        Sfx.Play(Sfx.Id.Whoosh, 0.35f, 1.4f);
        Juice.Smoke(Pos, 0.6f, 3); // launch puff
        Juice.SparksDir(Pos, Vector2.up, 50f, new Color(1f, 0.7f, 0.3f), 5, 4f, 0.14f);

        EnemyHealth target = Toughest(9f);
        int n = Count[L];
        for (int i = 0; i < n; i++)
        {
            // launch in a fan, then curve in toward the target
            Vector2 launch = Rotate(Vector2.up, (i - (n - 1) / 2f) * 30f + Random.Range(-10f, 10f));
            Launch(launch, target);
        }
    }

    private EnemyHealth Toughest(float range)
    {
        EnemyHealth best = null;
        foreach (Collider2D c in EnemiesNear(Pos, range))
        {
            if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead && (best == null || e.CurrentHealth > best.CurrentHealth)) best = e;
        }
        return best;
    }

    private void Launch(Vector2 dir, EnemyHealth target)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/missiles"), Color.white, 0.6f, Pos);
        s.trail = new Color(1f, 0.6f, 0.2f);
        s.velocity = dir * Speed;
        s.life = 3f;
        s.hitRadius = 0.3f;
        s.pierce = 1;

        float damage = Dmg(Damage[L]);
        float radius = Area(Radius[L]);
        int life = target != null ? target.Life : 0;

        s.onUpdate = shot =>
        {
            shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(shot.velocity.y, shot.velocity.x) * Mathf.Rad2Deg - 90f); // art points up
            Exhaust(shot);
            if (target == null || target.IsDead || target.Life != life) return; // target gone: fly straight
            Vector2 want = ((Vector2)target.transform.position - shot.Position).normalized;
            float angle = Vector2.SignedAngle(shot.velocity, want);
            shot.velocity = Rotate(shot.velocity, Mathf.Clamp(angle, -TurnRate * Time.deltaTime, TurnRate * Time.deltaTime));
        };
        // the blast damages everything around, including the enemy we touched
        s.onHit = (shot, e) => { Explode(shot.Position, radius, damage); OnBlast(shot.Position, radius, damage); shot.Release(); };
        s.onExpire = shot => { Explode(shot.Position, radius, damage); OnBlast(shot.Position, radius, damage); };
    }

    // Flickering flame out of the tail and puffs of smoke behind it; call every frame (super missiles too).
    public static void Exhaust(Shot shot)
    {
        Vector2 back = -shot.velocity.normalized;
        Juice.SparksDir(shot.Position + back * 0.3f, back, 18f, Random.value < 0.5f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.1f), 1, 3f, 0.16f);
        if (Time.frameCount % 2 == 0) Juice.Smoke(shot.Position + back * 0.4f, 0.35f, 1);
    }

    // Chain Missiles collab; Cluster Barrage overrides this to scatter mines instead.
    protected virtual void OnBlast(Vector2 pos, float radius, float damage)
    {
        if (PlayerCollabs.Has("missilechain") && PlayerSkills.Instance != null) PlayerSkills.Instance.Chain(pos, radius, damage);
    }
}
