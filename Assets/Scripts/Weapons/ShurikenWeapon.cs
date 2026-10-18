using UnityEngine;

public class ShurikenWeapon : Weapon
{
    private static readonly int[] Count = { 2, 2, 2, 3, 3, 3, 4 };
    private static readonly int[] Bounces = { 2, 2, 3, 3, 4, 4, 6 };
    private static readonly float[] Damage = { 7, 8, 8, 9, 9, 10, 12 };
    private const float Speed = 14f;
    private const float BounceRange = 4f;

    private void Update()
    {
        if (!Ready(1.6f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.3f, 1.5f);

        for (int i = 0; i < Count[L]; i++)
        {
            Transform target = RandomEnemy(8f);
            Vector2 dir = target != null ? ((Vector2)target.position - Pos).normalized : Rotate(Facing, Random.Range(-30f, 30f));
            Throw(dir);
        }
    }

    protected virtual Color StarTint => Color.white;
    protected virtual void OnStarHit(Vector2 at) { }

    private void Throw(Vector2 dir)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/shuriken"), StarTint, 0.7f, Pos);
        s.trail = new Color(0.75f, 0.85f, 1f, 0.6f);
        s.trailSize = 0.1f;
        s.velocity = dir * Speed;
        s.life = 1.5f;
        s.hitRadius = 0.45f;
        s.spin = 1440f;
        s.ghost = new Color(StarTint.r * 0.6f, StarTint.g * 0.8f, StarTint.b, 0.45f);

        float damage = Dmg(Damage[L]);
        int bouncesLeft = Bounces[L];

        s.onHit = (shot, e) =>
        {
            Juice.SparksDir(shot.Position, shot.velocity, 40f, new Color(1f, 0.95f, 0.75f), 5, 7f, 0.12f);
            ExplosionFx.Ring(shot.Position, 0.45f, new Color(0.85f, 0.92f, 1f), 0.15f);
            Hit(e, damage, shot.Position);
            OnStarHit(shot.Position);
            if (bouncesLeft-- <= 0) { shot.Release(); return; }

            Transform next = null;
            float best = float.MaxValue;
            foreach (Collider2D c in EnemiesNear(shot.Position, BounceRange))
            {
                if (!c.TryGetComponent(out EnemyHealth other) || other.IsDead || shot.HasHit(other)) continue;
                float d = ((Vector2)c.transform.position - shot.Position).sqrMagnitude;
                if (d < best) { best = d; next = c.transform; }
            }
            if (next == null) { shot.Release(); return; }

            shot.velocity = ((Vector2)next.position - shot.Position).normalized * Speed;
            shot.age = 0f;
        };
    }
}
