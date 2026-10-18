using UnityEngine;

public class GravityWheelWeapon : Weapon
{
    private const int Count = 2;
    private const float Size = 3.5f;
    private const float Damage = 30f;
    private const float Range = 6.5f;
    private const float Speed = 10f;
    private const float PullRadius = 3f;
    private const float PullSpeed = 5f;
    private const float PopDamage = 50f;
    private const float PopRadius = 2.5f;

    private void Update()
    {
        if (!Ready(2.5f)) return;

        Vector2 aim = AimDirection(9f);
        for (int i = 0; i < Count; i++) Throw(Rotate(aim, (i - (Count - 1) / 2f) * 40f));
    }

    private void Throw(Vector2 dir)
    {
        float size = Area(Size);
        Shot s = Shot.Spawn(Shapes.Crescent, new Color(0.6f, 0.4f, 1f), 0.6f * size, Pos);
        s.trail = new Color(0.7f, 0.45f, 1f, 0.7f);
        s.velocity = dir * Speed;
        s.life = 5f;
        s.hitRadius = 0.45f * size;
        s.spin = -720f;
        s.ghost = new Color(0.55f, 0.35f, 1f, 0.18f);

        float outTime = Range / Speed;
        float damage = Dmg(Damage), pull = Area(PullRadius);
        bool returning = false;
        Transform player = transform;

        s.onUpdate = shot =>
        {
            Vector2 edge = Random.insideUnitCircle.normalized;
            Juice.SparksDir(shot.Position + edge * pull, -edge, 10f, Random.value < 0.5f ? new Color(0.45f, 0.25f, 0.9f) : new Color(0.15f, 0.05f, 0.3f), 1, pull * 2.5f, 0.14f);

            foreach (Collider2D c in EnemiesNear(shot.Position, pull))
            {
                if (!c.TryGetComponent(out EnemyAI cat)) continue;
                Vector2 to = shot.Position - (Vector2)c.transform.position;
                if (to.sqrMagnitude > 0.04f) cat.Pull(to.normalized * PullSpeed);
            }

            if (!returning && shot.age >= outTime)
            {
                returning = true;
                shot.ClearHits();
                Explode(shot.Position, Area(PopRadius), Dmg(PopDamage));
                ExplosionFx.Ring(shot.Position, Area(PopRadius) * 1.2f, new Color(0.7f, 0.45f, 1f), 0.35f);
            }
            if (!returning) return;

            Vector2 toPlayer = (Vector2)player.position - shot.Position;
            if (toPlayer.magnitude < 0.5f) { shot.Release(); return; }
            shot.velocity = toPlayer.normalized * (Speed + 2f);
        };
        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position);
            Juice.SparksDir(e.transform.position, shot.velocity, 45f, new Color(0.75f, 0.55f, 1f), 4, 6f, 0.14f);
        };
    }
}
