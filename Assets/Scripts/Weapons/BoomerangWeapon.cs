using UnityEngine;

public class BoomerangWeapon : Weapon
{
    private static readonly int[] Count = { 2, 2, 3, 3, 3, 4, 5 };
    private static readonly float[] Damage = { 10, 13, 13, 13, 16, 16, 22 };
    private static readonly float[] Size = { 2f, 2f, 2f, 2.5f, 2.5f, 2.5f, 3f };
    private static readonly float[] Range = { 5f, 5f, 5f, 5f, 5.5f, 5.5f, 6.5f };
    private const float Speed = 9f;

    private void Update()
    {
        if (!Ready(1.8f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.35f);

        Vector2 aim = AimDirection(8f);
        int n = Count[L];
        for (int i = 0; i < n; i++) Throw(Rotate(aim, (i - (n - 1) / 2f) * 25f));
    }

    private void Throw(Vector2 dir)
    {
        float size = Size[L];
        Shot s = Shot.Spawn(Shapes.Art("Icons/boomerang"), Color.white, 0.55f * size, Pos);
        s.trail = new Color(1f, 0.7f, 0.3f, 0.6f);
        s.velocity = dir * Speed;
        s.life = 4f;
        s.hitRadius = 0.35f * size;
        s.spin = -1080f;
        s.ghost = new Color(1f, 0.6f, 0.25f, 0.35f);

        float outTime = Range[L] / Speed;
        float damage = Dmg(Damage[L]);
        bool returning = false;
        Transform player = transform;

        s.onUpdate = shot =>
        {
            if (!returning && shot.age >= outTime)
            {
                returning = true;
                shot.ClearHits();
                ExplosionFx.Ring(shot.Position, 0.35f * size, new Color(1f, 1f, 1f, 0.6f), 0.2f);
            }
            if (!returning) return;

            Vector2 toPlayer = (Vector2)player.position - shot.Position;
            if (toPlayer.magnitude < 0.4f)
            {
                Juice.Sparks(shot.Position, new Color(1f, 0.8f, 0.45f), 4, 3f, 0.1f);
                shot.Release();
                return;
            }
            shot.velocity = toPlayer.normalized * (Speed + 2f);
        };
        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position);
            Juice.SparksDir(e.transform.position, shot.velocity, 45f, new Color(1f, 0.75f, 0.35f), 4, 6f, 0.14f);
        };
    }
}
