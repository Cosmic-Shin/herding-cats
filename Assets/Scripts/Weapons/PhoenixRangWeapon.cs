using UnityEngine;

public class PhoenixRangWeapon : Weapon
{
    private const int Count = 3;
    private const float Size = 2.8f;
    private const float Damage = 24f;
    private const float Range = 6.5f;
    private const float Speed = 10f;
    private const float TrailEvery = 0.12f;
    private const float TrailTick = 7f;
    private const float BurstDamage = 40f;
    private const float BurstRadius = 1.8f;
    private static readonly Color Flame = new Color(1f, 0.5f, 0.15f);

    private void Update()
    {
        if (!Ready(2f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.4f, 0.8f);
        Vector2 aim = AimDirection(8f);
        for (int i = 0; i < Count; i++) Throw(Rotate(aim, (i - (Count - 1) / 2f) * 30f));
    }

    private void Throw(Vector2 dir)
    {
        float size = Area(Size);
        Shot s = Shot.Spawn(Shapes.Art("Icons/boomerang"), new Color(1f, 0.75f, 0.45f), 0.55f * size, Pos);
        s.velocity = dir * Speed;
        s.life = 4f;
        s.hitRadius = 0.35f * size;
        s.spin = -1080f;
        s.ghost = new Color(1f, 0.4f, 0.1f, 0.45f);
        s.trail = new Color(1f, 0.75f, 0.25f);

        float outTime = Range / Speed, damage = Dmg(Damage), tick = Dmg(TrailTick);
        float nextFire = 0.1f;
        bool returning = false;
        Transform player = transform;

        s.onUpdate = shot =>
        {
            Juice.SparksDir(shot.Position, Vector2.up, 30f, Random.value < 0.5f ? Flame : new Color(1f, 0.85f, 0.35f), 1, 2.5f, 0.15f);
            if (shot.age >= nextFire)
            {
                nextFire = shot.age + TrailEvery;
                BurningGround.Spawn(shot.Position, Area(0.6f), 2.5f, tick);
            }

            if (!returning && shot.age >= outTime)
            {
                returning = true;
                shot.ClearHits();
                Explode(shot.Position, Area(BurstRadius), Dmg(BurstDamage));
                BurningGround.Spawn(shot.Position, Area(BurstRadius), 3f, tick);
            }
            if (!returning) return;

            Vector2 toPlayer = (Vector2)player.position - shot.Position;
            if (toPlayer.magnitude < 0.5f) { shot.Release(); return; }
            shot.velocity = toPlayer.normalized * (Speed + 2f);
        };
        s.onHit = (shot, e) =>
        {
            e.MarkBurning();
            Hit(e, damage, shot.Position);
            Juice.SparksDir(e.transform.position, shot.velocity, 45f, Flame, 5, 6f, 0.15f);
        };
    }
}
