using UnityEngine;

// Collab: Boomerang + Shuriken. Giant blades ricochet between enemies like shurikens,
// then fly back to you like boomerangs, cutting through everything on the way home.
public class ChakramStormWeapon : Weapon
{
    private const int Count = 4;
    private const int Bounces = 5;
    private const float Damage = 26f;
    private const float Size = 2.2f;
    private const float Speed = 13f;
    private const float BounceRange = 5f;
    private const float MaxOutTime = 1.2f; // fly back if a leg finds nothing for this long

    private void Update()
    {
        if (!Ready(1.6f)) return;

        for (int i = 0; i < Count; i++)
        {
            Transform target = RandomEnemy(9f);
            Vector2 dir = target != null ? ((Vector2)target.position - Pos).normalized : Rotate(Facing, i * 360f / Count);
            Throw(dir);
        }
    }

    private void Throw(Vector2 dir)
    {
        float size = Area(Size);
        Shot s = Shot.Spawn(Shapes.Crescent, new Color(1f, 0.85f, 0.45f), 0.6f * size, Pos);
        s.trail = new Color(1f, 0.85f, 0.45f, 0.6f);
        s.velocity = dir * Speed;
        s.life = 8f;
        s.hitRadius = 0.4f * size;
        s.spin = -1440f;
        s.ghost = new Color(1f, 0.8f, 0.35f, 0.4f); // whirling golden blur

        float damage = Dmg(Damage);
        int bouncesLeft = Bounces;
        bool returning = false;
        Transform player = transform;

        void GoHome(Shot shot)
        {
            returning = true;
            shot.ClearHits(); // can hit everything again on the way back
        }

        s.onUpdate = shot =>
        {
            if (!returning && shot.age >= MaxOutTime) GoHome(shot);
            if (!returning) return;
            Vector2 toPlayer = (Vector2)player.position - shot.Position;
            if (toPlayer.magnitude < 0.5f) { shot.Release(); return; }
            shot.velocity = toPlayer.normalized * (Speed + 3f);
        };

        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position);
            Juice.SparksDir(e.transform.position, shot.velocity, 45f, new Color(1f, 0.9f, 0.55f), 5, 7f, 0.14f);
            if (returning) return;
            if (bouncesLeft-- <= 0) { GoHome(shot); return; }

            Transform next = null;
            float best = float.MaxValue;
            foreach (Collider2D c in EnemiesNear(shot.Position, BounceRange))
            {
                if (!c.TryGetComponent(out EnemyHealth other) || other.IsDead || shot.HasHit(other)) continue;
                float d = ((Vector2)c.transform.position - shot.Position).sqrMagnitude;
                if (d < best) { best = d; next = c.transform; }
            }
            if (next == null) { GoHome(shot); return; }

            shot.velocity = ((Vector2)next.position - shot.Position).normalized * Speed;
            shot.age = 0f; // fresh time for the new leg
        };
    }
}
